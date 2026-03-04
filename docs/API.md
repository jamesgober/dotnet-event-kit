# API Reference

## Interfaces

### `IEventBus`

Publishes events to registered handlers within the current process.

```csharp
public interface IEventBus
{
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
```

Inject `IEventBus` into any service that needs to raise events. Thread-safe for concurrent publish calls.

**Exceptions:**
- `ArgumentNullException` — if the event is `null`
- `OperationCanceledException` — if the cancellation token fires
- `AggregateException` — when using `EventErrorPolicy.Aggregate` and one or more handlers fail

---

### `IEventHandler<TEvent>`

Handles events of a specific type. Implementations are resolved from DI per publish.

```csharp
public interface IEventHandler<in TEvent> where TEvent : notnull
{
    ValueTask HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
```

**Example:**
```csharp
public sealed class WelcomeEmailHandler(IEmailService email) : IEventHandler<UserRegistered>
{
    public async ValueTask HandleAsync(UserRegistered @event, CancellationToken cancellationToken)
    {
        await email.SendWelcomeAsync(@event.Email, cancellationToken);
    }
}
```

---

### `IEventMiddleware`

Wraps event dispatch for cross-cutting concerns (logging, timing, retries).

```csharp
public interface IEventMiddleware
{
    ValueTask InvokeAsync<TEvent>(TEvent @event, EventDispatchDelegate next, CancellationToken cancellationToken)
        where TEvent : notnull;
}
```

Middleware executes in registration order. Call `next()` to continue the pipeline.

**Example:**
```csharp
public sealed class TimingMiddleware(ILogger<TimingMiddleware> logger) : IEventMiddleware
{
    public async ValueTask InvokeAsync<TEvent>(
        TEvent @event, EventDispatchDelegate next, CancellationToken cancellationToken)
        where TEvent : notnull
    {
        var sw = Stopwatch.StartNew();
        await next();
        logger.LogInformation("Dispatched {EventType} in {Elapsed}ms", typeof(TEvent).Name, sw.ElapsedMilliseconds);
    }
}
```

---

## Types

### `EventDispatchDelegate`

Delegate representing the next step in the event dispatch pipeline. Used by `IEventMiddleware`.

```csharp
public delegate ValueTask EventDispatchDelegate();
```

### `EventKitOptions`

Configuration for the event bus.

| Property | Type | Default | Description |
|---|---|---|---|
| `OnError` | `EventErrorPolicy` | `LogAndContinue` | How handler exceptions are handled |
| `MaxParallelHandlers` | `int` | `1` | Max concurrent handlers per publish. `1` = sequential |
| `DeadLetterHandler` | `Func<DeadLetterEvent, CancellationToken, ValueTask>?` | `null` | Callback for events with no matching handlers |

---

### `EventErrorPolicy`

| Value | Behavior |
|---|---|
| `StopOnFirst` | Stop on first handler failure, propagate exception |
| `LogAndContinue` | Log and continue to remaining handlers |
| `Aggregate` | Run all handlers, throw `AggregateException` if any fail |

---

### `EventSubscription`

Registration metadata for a handler. Created automatically by `AddEventHandler`.

| Property | Type | Description |
|---|---|---|
| `EventType` | `Type` | The event type this handler targets |
| `HandlerType` | `Type` | The handler implementation type |
| `Priority` | `int` | Execution order (lower = first) |
| `HasFilter` | `bool` | Whether a filter predicate is attached |

---

### `DeadLetterEvent`

Wraps an event that had no matching handlers.

| Property | Type | Description |
|---|---|---|
| `Event` | `object` | The original event instance |
| `EventType` | `Type` | Runtime type of the event |
| `Timestamp` | `DateTimeOffset` | When the dead letter was created (UTC) |

---

## Registration

### `AddEventKit`

Registers the event bus and its dependencies.

```csharp
services.AddEventKit(options =>
{
    options.OnError = EventErrorPolicy.LogAndContinue;
    options.MaxParallelHandlers = 4;
    options.DeadLetterHandler = (dl, ct) =>
    {
        logger.LogWarning("No handlers for {EventType}", dl.EventType.Name);
        return default;
    };
});
```

### `AddEventHandler<TEvent, THandler>`

Registers a handler for a specific event type.

```csharp
services.AddEventHandler<UserRegistered, WelcomeEmailHandler>();
services.AddEventHandler<UserRegistered, AuditLogHandler>(priority: 10);
services.AddEventHandler<OrderPlaced, InventoryHandler>(
    filter: e => e.Total > 100,
    lifetime: ServiceLifetime.Scoped);
```

| Parameter | Default | Description |
|---|---|---|
| `priority` | `0` | Lower values run first |
| `filter` | `null` | Predicate to skip non-matching events |
| `lifetime` | `Transient` | DI lifetime for the handler |

### `AddEventMiddleware<T>`

Registers middleware (singleton). Middleware runs in registration order.

```csharp
services.AddEventMiddleware<TimingMiddleware>();
services.AddEventMiddleware<RetryMiddleware>();
```
