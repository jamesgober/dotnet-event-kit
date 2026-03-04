# Getting Started

## Installation

```bash
dotnet add package JG.EventKit
```

## Setup

Register EventKit in your DI container during startup:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEventKit(options =>
{
    options.OnError = EventErrorPolicy.LogAndContinue;
});
```

## Define Events

Events are plain C# types. Records work well since events should be immutable:

```csharp
public record UserRegistered(string UserId, string Email, DateTimeOffset RegisteredAt);

public record OrderPlaced(string OrderId, decimal Total, IReadOnlyList<string> ItemIds);

public record PaymentFailed(string OrderId, string Reason);
```

## Create Handlers

Implement `IEventHandler<TEvent>` for each event you want to handle. Handlers are resolved
from DI, so inject any dependencies through the constructor:

```csharp
public sealed class WelcomeEmailHandler(IEmailService email) : IEventHandler<UserRegistered>
{
    public async ValueTask HandleAsync(UserRegistered @event, CancellationToken cancellationToken)
    {
        await email.SendWelcomeAsync(@event.Email, cancellationToken);
    }
}

public sealed class AuditLogHandler(IAuditRepository audit) : IEventHandler<UserRegistered>
{
    public async ValueTask HandleAsync(UserRegistered @event, CancellationToken cancellationToken)
    {
        await audit.LogAsync($"User {@ event.UserId} registered", cancellationToken);
    }
}
```

## Register Handlers

Register each handler with an optional priority. Lower priority values execute first:

```csharp
builder.Services.AddEventHandler<UserRegistered, AuditLogHandler>(priority: 0);
builder.Services.AddEventHandler<UserRegistered, WelcomeEmailHandler>(priority: 10);
builder.Services.AddEventHandler<OrderPlaced, InventoryReservationHandler>();
```

## Publish Events

Inject `IEventBus` and call `PublishAsync`:

```csharp
public sealed class RegistrationService(IEventBus bus, IUserRepository users)
{
    public async Task RegisterAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(email, cancellationToken);

        await bus.PublishAsync(
            new UserRegistered(user.Id, email, DateTimeOffset.UtcNow),
            cancellationToken);
    }
}
```

## Filtering

Skip handler invocation for events that don't match a predicate:

```csharp
// Only handle orders over $100
builder.Services.AddEventHandler<OrderPlaced, HighValueOrderHandler>(
    filter: e => e.Total > 100);
```

The handler is never called for orders with a total of $100 or less.

## Error Policies

Control what happens when a handler throws:

```csharp
// Stop dispatching on first failure (default behavior for critical flows)
builder.Services.AddEventKit(o => o.OnError = EventErrorPolicy.StopOnFirst);

// Log and continue (default — best for independent side effects)
builder.Services.AddEventKit(o => o.OnError = EventErrorPolicy.LogAndContinue);

// Collect all errors and throw AggregateException
builder.Services.AddEventKit(o => o.OnError = EventErrorPolicy.Aggregate);
```

## Parallel Dispatch

Run handlers concurrently when order doesn't matter:

```csharp
builder.Services.AddEventKit(o => o.MaxParallelHandlers = 4);
```

Handlers still respect priority ordering within each batch.

## Dead Letters

Capture events that have no registered handlers:

```csharp
builder.Services.AddEventKit(options =>
{
    options.DeadLetterHandler = (deadLetter, ct) =>
    {
        Console.WriteLine($"No handlers for {deadLetter.EventType.Name}");
        return default;
    };
});
```

## Middleware

Add cross-cutting behavior that wraps every event dispatch:

```csharp
public sealed class LoggingMiddleware(ILogger<LoggingMiddleware> logger) : IEventMiddleware
{
    public async ValueTask InvokeAsync<TEvent>(
        TEvent @event,
        EventDispatchDelegate next,
        CancellationToken cancellationToken) where TEvent : notnull
    {
        logger.LogInformation("Dispatching {EventType}", typeof(TEvent).Name);
        await next();
        logger.LogInformation("Dispatched {EventType}", typeof(TEvent).Name);
    }
}

// Register — middleware runs in registration order
builder.Services.AddEventMiddleware<LoggingMiddleware>();
```

## Handler Lifetimes

Control how handlers are resolved from DI:

```csharp
// Transient (default) — new instance per publish
builder.Services.AddEventHandler<UserRegistered, WelcomeEmailHandler>();

// Scoped — shared within the publish scope
builder.Services.AddEventHandler<OrderPlaced, OrderHandler>(
    lifetime: ServiceLifetime.Scoped);

// Singleton — single instance for the application lifetime
builder.Services.AddEventHandler<HealthCheck, HealthCheckHandler>(
    lifetime: ServiceLifetime.Singleton);
```

## Full Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure EventKit
builder.Services.AddEventKit(options =>
{
    options.OnError = EventErrorPolicy.LogAndContinue;
    options.MaxParallelHandlers = 1;
});

// Register handlers
builder.Services.AddEventHandler<UserRegistered, WelcomeEmailHandler>(priority: 10);
builder.Services.AddEventHandler<UserRegistered, AuditLogHandler>(priority: 0);
builder.Services.AddEventHandler<OrderPlaced, InventoryHandler>();

// Register middleware
builder.Services.AddEventMiddleware<LoggingMiddleware>();

var app = builder.Build();

app.MapPost("/register", async (RegisterRequest req, IEventBus bus, CancellationToken ct) =>
{
    var userId = Guid.NewGuid().ToString();
    await bus.PublishAsync(new UserRegistered(userId, req.Email, DateTimeOffset.UtcNow), ct);
    return Results.Created($"/users/{userId}", new { id = userId });
});

app.Run();
```
