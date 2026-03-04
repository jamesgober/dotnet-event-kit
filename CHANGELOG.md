# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2023-10-05

### Added
- `IEventBus` interface with `PublishAsync<TEvent>` for strongly-typed event dispatch
- `IEventHandler<TEvent>` interface for async event handler implementations
- `IEventMiddleware` interface for wrapping event dispatch with cross-cutting concerns
- `EventDispatchDelegate` for middleware pipeline continuation
- `EventKitOptions` with configurable error policy, parallelism, and dead letter handling
- `EventErrorPolicy` enum: `StopOnFirst`, `LogAndContinue`, `Aggregate`
- `EventSubscription` metadata type with priority ordering and filter predicates
- `DeadLetterEvent` wrapper for events with no registered handlers
- `AddEventKit()` extension method for DI registration with options
- `AddEventHandler<TEvent, THandler>()` with priority, filter, and lifetime support
- `AddEventMiddleware<T>()` for pipeline middleware registration
- Sequential and parallel handler dispatch with configurable concurrency
- Handler priority ordering (lower values execute first, stable sort on equal priority)
- Event filtering via per-handler predicates
- Scoped handler resolution per publish for DI lifetime support
- Dead letter handler exception safety with logging
- Options validation at construction (invalid `EventErrorPolicy` values rejected)
- High-performance logging via source-generated `LoggerMessage` delegates
- SourceLink for NuGet package debugging
- CI workflow with multi-platform matrix (Ubuntu, Windows, macOS)
- NuGet publish workflow triggered on GitHub releases
- BenchmarkDotNet project for critical path performance validation
- API reference and getting-started documentation

## [0.1.0] - YYYY-MM-DD

### Added
- Initial project scaffolding, documentation structure, and license

[Unreleased]: https://github.com/jamesgober/dotnet-event-kit/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/jamesgober/dotnet-event-kit/releases/tag/v1.0.0
[0.1.0]: https://github.com/jamesgober/dotnet-event-kit/releases/tag/v0.1.0
