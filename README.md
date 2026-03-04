# dotnet-event-kit

[![NuGet](https://img.shields.io/nuget/v/JG.EventKit?logo=nuget)](https://www.nuget.org/packages/JG.EventKit)
[![Downloads](https://img.shields.io/nuget/dt/JG.EventKit?color=%230099ff&logo=nuget)](https://www.nuget.org/packages/JG.EventKit)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](./LICENSE)
[![CI](https://github.com/jamesgober/dotnet-event-kit/actions/workflows/ci.yml/badge.svg)](https://github.com/jamesgober/dotnet-event-kit/actions)

---

A lightweight, high-performance in-process event bus for .NET applications. Publish strongly-typed events, subscribe async handlers, and decouple components without external message brokers.

## Features

- **Strongly-typed events** — publish and subscribe with full type safety, no casting or string keys
- **Async-native handlers** — all handlers are `ValueTask`-based with `CancellationToken` support
- **Ordered dispatch** — handlers execute in registration order with configurable parallelism
- **Handler priorities** — control execution order across independently registered handlers
- **Scoped & singleton handlers** — register handlers with DI lifetime management
- **Error isolation** — one handler failure doesn't kill other subscribers; configurable error policies
- **Event filtering** — subscribe to events matching a predicate without processing every publish
- **Dead letter support** — capture events with no registered handlers for debugging
- **Middleware pipeline** — wrap event dispatch with cross-cutting concerns (logging, timing, retry)
- **Zero external dependencies** — built entirely on .NET 8 primitives

## Installation

```bash
dotnet add package JG.EventKit
```

## Quick Start

```csharp
// Register in DI
builder.Services.AddEventKit(options =>
{
    options.OnError = EventErrorPolicy.LogAndContinue;
});

// Define an event
public record UserRegistered(string UserId, string Email, DateTime Timestamp);

// Subscribe a handler
builder.Services.AddEventHandler<UserRegistered, WelcomeEmailHandler>();

public class WelcomeEmailHandler : IEventHandler<UserRegistered>
{
    public async ValueTask HandleAsync(UserRegistered e, CancellationToken ct)
    {
        await SendWelcomeEmail(e.Email, ct);
    }
}

// Publish from anywhere
public class RegistrationService(IEventBus bus)
{
    public async Task RegisterAsync(string email)
    {
        var user = CreateUser(email);
        await bus.PublishAsync(new UserRegistered(user.Id, email, DateTime.UtcNow));
    }
}
```

## Documentation

- **[Getting Started](./docs/GETTING-STARTED.md)** — Setup, handlers, middleware, and full examples
- **[API Reference](./docs/API.md)** — Complete API surface with type tables and code samples

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

Licensed under the Apache License 2.0. See [LICENSE](./LICENSE) for details.

---

**Ready to get started?** Install via NuGet and check out the [API reference](./docs/API.md).
