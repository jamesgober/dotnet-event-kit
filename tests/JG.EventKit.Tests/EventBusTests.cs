using JG.EventKit;
using JG.EventKit.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JG.EventKit.Tests;

public sealed class EventBusTests
{
    private sealed record TestEvent(string Value);
    private sealed record UnhandledEvent(int Id);

    #region Test Handlers

    private sealed class RecordingHandler(InvocationLog log) : IEventHandler<TestEvent>
    {
        public ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            log.Add($"{nameof(RecordingHandler)}:{@event.Value}");
            return default;
        }
    }

    private sealed class SecondHandler(InvocationLog log) : IEventHandler<TestEvent>
    {
        public ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            log.Add($"{nameof(SecondHandler)}:{@event.Value}");
            return default;
        }
    }

    private sealed class ThirdHandler(InvocationLog log) : IEventHandler<TestEvent>
    {
        public ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            log.Add($"{nameof(ThirdHandler)}:{@event.Value}");
            return default;
        }
    }

    private sealed class FailingHandler : IEventHandler<TestEvent>
    {
        public ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException($"Handler failed for {nameof(FailingHandler)}");
        }
    }

    private sealed class AnotherFailingHandler : IEventHandler<TestEvent>
    {
        public ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            throw new ArgumentException($"Handler failed for {nameof(AnotherFailingHandler)}");
        }
    }

    private sealed class CancellingHandler : IEventHandler<TestEvent>
    {
        public ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            throw new OperationCanceledException("Handler cancelled itself");
        }
    }

    private sealed class SlowHandler(InvocationLog log) : IEventHandler<TestEvent>
    {
        public async ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            log.Add($"{nameof(SlowHandler)}:{@event.Value}");
        }
    }

    private sealed class ScopedInstanceTracker(InvocationLog log) : IEventHandler<TestEvent>
    {
        private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

        public ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            log.Add($"ScopedInstance:{_instanceId}");
            return default;
        }
    }

    private sealed class InvocationLog
    {
        private readonly List<string> _entries = [];
        public IReadOnlyList<string> Entries => _entries;
        public void Add(string entry) { lock (_entries) { _entries.Add(entry); } }
    }

    #endregion

    #region Publish — Happy Path

    [Fact]
    public async Task PublishAsync_SingleHandler_InvokesHandler()
    {
        var (bus, log) = CreateBus(s =>
            s.AddEventHandler<TestEvent, RecordingHandler>());

        await bus.PublishAsync(new TestEvent("hello"));

        log.Entries.Should().ContainSingle()
            .Which.Should().Be("RecordingHandler:hello");
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_InvokesAll()
    {
        var (bus, log) = CreateBus(s =>
        {
            s.AddEventHandler<TestEvent, RecordingHandler>();
            s.AddEventHandler<TestEvent, SecondHandler>();
        });

        await bus.PublishAsync(new TestEvent("test"));

        log.Entries.Should().HaveCount(2);
        log.Entries.Should().Contain("RecordingHandler:test");
        log.Entries.Should().Contain("SecondHandler:test");
    }

    #endregion

    #region Publish — Priority Ordering

    [Fact]
    public async Task PublishAsync_HandlersWithPriority_ExecutesInPriorityOrder()
    {
        var (bus, log) = CreateBus(s =>
        {
            s.AddEventHandler<TestEvent, ThirdHandler>(priority: 30);
            s.AddEventHandler<TestEvent, RecordingHandler>(priority: 10);
            s.AddEventHandler<TestEvent, SecondHandler>(priority: 20);
        });

        await bus.PublishAsync(new TestEvent("ordered"));

        log.Entries.Should().HaveCount(3);
        log.Entries[0].Should().StartWith("RecordingHandler:");
        log.Entries[1].Should().StartWith("SecondHandler:");
        log.Entries[2].Should().StartWith("ThirdHandler:");
    }

    #endregion

    #region Publish — Filtering

    [Fact]
    public async Task PublishAsync_WithFilter_SkipsNonMatchingEvents()
    {
        var (bus, log) = CreateBus(s =>
        {
            s.AddEventHandler<TestEvent, RecordingHandler>(
                filter: e => e.Value == "match");
            s.AddEventHandler<TestEvent, SecondHandler>();
        });

        await bus.PublishAsync(new TestEvent("no-match"));

        log.Entries.Should().ContainSingle()
            .Which.Should().StartWith("SecondHandler:");
    }

    [Fact]
    public async Task PublishAsync_WithFilterMatch_InvokesHandler()
    {
        var (bus, log) = CreateBus(s =>
            s.AddEventHandler<TestEvent, RecordingHandler>(
                filter: e => e.Value == "match"));

        await bus.PublishAsync(new TestEvent("match"));

        log.Entries.Should().ContainSingle()
            .Which.Should().Be("RecordingHandler:match");
    }

    [Fact]
    public async Task PublishAsync_AllFiltered_InvokesDeadLetter()
    {
        DeadLetterEvent? captured = null;

        var (bus, _) = CreateBus(
            s => s.AddEventHandler<TestEvent, RecordingHandler>(filter: _ => false),
            o =>
            {
                o.DeadLetterHandler = (dl, _) => { captured = dl; return default; };
            });

        await bus.PublishAsync(new TestEvent("filtered"));

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(typeof(TestEvent));
    }

    #endregion

    #region Publish — Dead Letter

    [Fact]
    public async Task PublishAsync_NoHandlers_InvokesDeadLetterHandler()
    {
        DeadLetterEvent? captured = null;

        var (bus, _) = CreateBus(options: o =>
        {
            o.DeadLetterHandler = (dl, _) => { captured = dl; return default; };
        });

        await bus.PublishAsync(new UnhandledEvent(42));

        captured.Should().NotBeNull();
        captured!.Event.Should().BeEquivalentTo(new UnhandledEvent(42));
        captured.EventType.Should().Be(typeof(UnhandledEvent));
        captured.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task PublishAsync_NoHandlersNoDeadLetter_CompletesSuccessfully()
    {
        var (bus, _) = CreateBus();

        Func<Task> act = async () => await bus.PublishAsync(new UnhandledEvent(1));

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Publish — Error Policies

    [Fact]
    public async Task PublishAsync_StopOnFirst_ThrowsOnFirstFailure()
    {
        var (bus, log) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, FailingHandler>(priority: 0);
                s.AddEventHandler<TestEvent, RecordingHandler>(priority: 10);
            },
            o => o.OnError = EventErrorPolicy.StopOnFirst);

        var act = () => bus.PublishAsync(new TestEvent("fail")).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
        log.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_LogAndContinue_ContinuesAfterFailure()
    {
        var (bus, log) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, FailingHandler>(priority: 0);
                s.AddEventHandler<TestEvent, RecordingHandler>(priority: 10);
            },
            o => o.OnError = EventErrorPolicy.LogAndContinue);

        await bus.PublishAsync(new TestEvent("continue"));

        log.Entries.Should().ContainSingle()
            .Which.Should().Be("RecordingHandler:continue");
    }

    [Fact]
    public async Task PublishAsync_Aggregate_CollectsAllExceptions()
    {
        var (bus, _) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, FailingHandler>(priority: 0);
                s.AddEventHandler<TestEvent, RecordingHandler>(priority: 10);
            },
            o => o.OnError = EventErrorPolicy.Aggregate);

        var act = () => bus.PublishAsync(new TestEvent("agg")).AsTask();

        var ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().ContainSingle()
            .Which.Should().BeOfType<InvalidOperationException>();
    }

    #endregion

    #region Publish — Argument Validation

    [Fact]
    public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
    {
        var (bus, _) = CreateBus();

        var act = () => bus.PublishAsync<TestEvent>(null!).AsTask();

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        var (bus, _) = CreateBus(s =>
            s.AddEventHandler<TestEvent, RecordingHandler>());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => bus.PublishAsync(new TestEvent("cancel"), cts.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PublishAsync_CancellationDuringHandler_ThrowsOperationCancelledException()
    {
        using var cts = new CancellationTokenSource();
        var (bus, log) = CreateBus(s =>
            s.AddEventHandler<TestEvent, SlowHandler>());

        // Cancel after a brief delay so the handler is already running.
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var act = () => bus.PublishAsync(new TestEvent("mid-cancel"), cts.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Publish — Middleware

    [Fact]
    public async Task PublishAsync_WithMiddleware_ExecutesPipeline()
    {
        var log = new InvocationLog();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventKit();
        services.AddSingleton(log);
        services.AddEventHandler<TestEvent, RecordingHandler>();
        services.AddSingleton<IEventMiddleware>(new TrackingMiddleware(log, "M1"));

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new TestEvent("mw"));

        log.Entries.Should().HaveCount(3);
        log.Entries[0].Should().Be("M1:before");
        log.Entries[1].Should().Be("RecordingHandler:mw");
        log.Entries[2].Should().Be("M1:after");
    }

    [Fact]
    public async Task PublishAsync_MultipleMiddleware_ExecutesInRegistrationOrder()
    {
        var log = new InvocationLog();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventKit();
        services.AddSingleton(log);
        services.AddEventHandler<TestEvent, RecordingHandler>();
        services.AddSingleton<IEventMiddleware>(new TrackingMiddleware(log, "Outer"));
        services.AddSingleton<IEventMiddleware>(new TrackingMiddleware(log, "Inner"));

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new TestEvent("chain"));

        log.Entries.Should().Equal(
            "Outer:before",
            "Inner:before",
            "RecordingHandler:chain",
            "Inner:after",
            "Outer:after");
    }

    private sealed class TrackingMiddleware(InvocationLog log, string name) : IEventMiddleware
    {
        public async ValueTask InvokeAsync<TEvent>(
            TEvent @event,
            EventDispatchDelegate next,
            CancellationToken cancellationToken) where TEvent : notnull
        {
            log.Add($"{name}:before");
            await next().ConfigureAwait(false);
            log.Add($"{name}:after");
        }
    }

    #endregion

    #region Publish — Parallel Dispatch

    [Fact]
    public async Task PublishAsync_ParallelHandlers_InvokesAllHandlers()
    {
        var (bus, log) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, RecordingHandler>();
                s.AddEventHandler<TestEvent, SecondHandler>();
                s.AddEventHandler<TestEvent, ThirdHandler>();
            },
            o => o.MaxParallelHandlers = 3);

        await bus.PublishAsync(new TestEvent("par"));

        log.Entries.Should().HaveCount(3);
        log.Entries.Should().Contain(e => e.StartsWith("RecordingHandler:", StringComparison.Ordinal));
        log.Entries.Should().Contain(e => e.StartsWith("SecondHandler:", StringComparison.Ordinal));
        log.Entries.Should().Contain(e => e.StartsWith("ThirdHandler:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishAsync_ParallelWithFilter_SkipsFilteredHandlers()
    {
        var (bus, log) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, RecordingHandler>(filter: e => e.Value == "yes");
                s.AddEventHandler<TestEvent, SecondHandler>();
            },
            o => o.MaxParallelHandlers = 2);

        await bus.PublishAsync(new TestEvent("no"));

        log.Entries.Should().ContainSingle()
            .Which.Should().StartWith("SecondHandler:");
    }

    [Fact]
    public async Task PublishAsync_ParallelNoMatchingHandlers_InvokesDeadLetter()
    {
        DeadLetterEvent? captured = null;

        var (bus, _) = CreateBus(
            s => s.AddEventHandler<TestEvent, RecordingHandler>(filter: _ => false),
            o =>
            {
                o.MaxParallelHandlers = 4;
                o.DeadLetterHandler = (dl, _) => { captured = dl; return default; };
            });

        await bus.PublishAsync(new TestEvent("nope"));

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(typeof(TestEvent));
    }

    #endregion

    #region Publish — Error Policy Edge Cases

    [Fact]
    public async Task PublishAsync_Aggregate_MultipleFailures_CollectsAll()
    {
        var (bus, _) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, FailingHandler>(priority: 0);
                s.AddEventHandler<TestEvent, AnotherFailingHandler>(priority: 10);
            },
            o => o.OnError = EventErrorPolicy.Aggregate);

        var act = () => bus.PublishAsync(new TestEvent("multi")).AsTask();

        var ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().HaveCount(2);
        ex.Which.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
        ex.Which.InnerExceptions.Should().Contain(e => e is ArgumentException);
    }

    [Fact]
    public async Task PublishAsync_HandlerThrowsOperationCancelled_AlwaysPropagates()
    {
        var (bus, _) = CreateBus(
            s => s.AddEventHandler<TestEvent, CancellingHandler>(),
            o => o.OnError = EventErrorPolicy.LogAndContinue);

        var act = () => bus.PublishAsync(new TestEvent("oce")).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Publish — Dead Letter Edge Cases

    [Fact]
    public async Task PublishAsync_DeadLetterHandlerThrows_LogsAndCompletes()
    {
        var (bus, _) = CreateBus(options: o =>
        {
            o.DeadLetterHandler = (_, _) => throw new InvalidOperationException("Dead letter boom");
        });

        Func<Task> act = async () => await bus.PublishAsync(new UnhandledEvent(99));

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Publish — Middleware Edge Cases

    [Fact]
    public async Task PublishAsync_MiddlewareShortCircuits_HandlersNotInvoked()
    {
        var log = new InvocationLog();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventKit();
        services.AddSingleton(log);
        services.AddEventHandler<TestEvent, RecordingHandler>();
        services.AddSingleton<IEventMiddleware>(new ShortCircuitMiddleware(log));

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new TestEvent("blocked"));

        log.Entries.Should().ContainSingle()
            .Which.Should().StartWith("ShortCircuit:");
    }

    [Fact]
    public async Task PublishAsync_MiddlewareThrows_PropagatesException()
    {
        var log = new InvocationLog();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventKit();
        services.AddSingleton(log);
        services.AddEventHandler<TestEvent, RecordingHandler>();
        services.AddSingleton<IEventMiddleware>(new ThrowingMiddleware());

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var act = () => bus.PublishAsync(new TestEvent("mw-fail")).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Middleware exploded");
        log.Entries.Should().BeEmpty();
    }

    private sealed class ShortCircuitMiddleware(InvocationLog log) : IEventMiddleware
    {
        public ValueTask InvokeAsync<TEvent>(
            TEvent @event, EventDispatchDelegate next, CancellationToken cancellationToken)
            where TEvent : notnull
        {
            log.Add($"ShortCircuit:{@event}");
            return default; // Don't call next()
        }
    }

    private sealed class ThrowingMiddleware : IEventMiddleware
    {
        public ValueTask InvokeAsync<TEvent>(
            TEvent @event, EventDispatchDelegate next, CancellationToken cancellationToken)
            where TEvent : notnull
        {
            throw new InvalidOperationException("Middleware exploded");
        }
    }

    #endregion

    #region Publish — Scoped Handlers

    [Fact]
    public async Task PublishAsync_TransientHandler_NewInstancePerPublish()
    {
        var (bus, log) = CreateBus(s =>
            s.AddEventHandler<TestEvent, ScopedInstanceTracker>(lifetime: ServiceLifetime.Transient));

        await bus.PublishAsync(new TestEvent("a"));
        await bus.PublishAsync(new TestEvent("b"));

        log.Entries.Should().HaveCount(2);
        var id1 = log.Entries[0].Split(':')[1];
        var id2 = log.Entries[1].Split(':')[1];
        id1.Should().NotBe(id2, "transient handlers should be different instances per publish");
    }

    #endregion

    #region Publish — Registration Order Stability

    [Fact]
    public async Task PublishAsync_EqualPriority_MaintainsRegistrationOrder()
    {
        var (bus, log) = CreateBus(s =>
        {
            s.AddEventHandler<TestEvent, RecordingHandler>(priority: 0);
            s.AddEventHandler<TestEvent, SecondHandler>(priority: 0);
            s.AddEventHandler<TestEvent, ThirdHandler>(priority: 0);
        });

        await bus.PublishAsync(new TestEvent("stable"));

        log.Entries.Should().HaveCount(3);
        log.Entries[0].Should().StartWith("RecordingHandler:");
        log.Entries[1].Should().StartWith("SecondHandler:");
        log.Entries[2].Should().StartWith("ThirdHandler:");
    }

    #endregion

    #region Publish — Parallel Error Policies

    [Fact]
    public async Task PublishAsync_ParallelLogAndContinue_CompletesAllHandlers()
    {
        var (bus, log) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, FailingHandler>(priority: 0);
                s.AddEventHandler<TestEvent, RecordingHandler>(priority: 10);
            },
            o =>
            {
                o.OnError = EventErrorPolicy.LogAndContinue;
                o.MaxParallelHandlers = 4;
            });

        await bus.PublishAsync(new TestEvent("par-continue"));

        log.Entries.Should().ContainSingle()
            .Which.Should().Be("RecordingHandler:par-continue");
    }

    [Fact]
    public async Task PublishAsync_ParallelAggregate_CollectsAllExceptions()
    {
        var (bus, _) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, FailingHandler>(priority: 0);
                s.AddEventHandler<TestEvent, AnotherFailingHandler>(priority: 10);
            },
            o =>
            {
                o.OnError = EventErrorPolicy.Aggregate;
                o.MaxParallelHandlers = 4;
            });

        var act = () => bus.PublishAsync(new TestEvent("par-agg")).AsTask();

        var ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task PublishAsync_ParallelStopOnFirst_ThrowsFirstException()
    {
        var (bus, _) = CreateBus(
            s =>
            {
                s.AddEventHandler<TestEvent, FailingHandler>(priority: 0);
                s.AddEventHandler<TestEvent, RecordingHandler>(priority: 10);
            },
            o =>
            {
                o.OnError = EventErrorPolicy.StopOnFirst;
                o.MaxParallelHandlers = 4;
            });

        var act = () => bus.PublishAsync(new TestEvent("par-stop")).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Publish — Parallel Filter + Error Interaction

    [Fact]
    public async Task PublishAsync_ParallelWithFilterAndFailure_LogsCorrectHandlerType()
    {
        // Regression: parallel dispatch with filters must align task indices with
        // filtered subscription indices, so error logging reports the right handler.
        var (bus, log) = CreateBus(
            s =>
            {
                // Handler at index 0 is filtered out — should be skipped entirely.
                s.AddEventHandler<TestEvent, RecordingHandler>(priority: 0, filter: _ => false);
                // Handler at index 1 passes filter and fails.
                s.AddEventHandler<TestEvent, FailingHandler>(priority: 10);
                // Handler at index 2 passes filter and succeeds.
                s.AddEventHandler<TestEvent, SecondHandler>(priority: 20);
            },
            o =>
            {
                o.OnError = EventErrorPolicy.LogAndContinue;
                o.MaxParallelHandlers = 4;
            });

        await bus.PublishAsync(new TestEvent("filter-error"));

        // SecondHandler should still run despite FailingHandler's error.
        log.Entries.Should().ContainSingle()
            .Which.Should().Be("SecondHandler:filter-error");
    }

    #endregion

    #region Publish — Options Validation

    [Fact]
    public void EventBus_InvalidErrorPolicy_ThrowsOnConstruction()
    {
        var act = () => CreateBus(options: o => o.OnError = (EventErrorPolicy)999);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Publish — Concurrent Access

    [Fact]
    public async Task PublishAsync_ConcurrentPublishes_AllComplete()
    {
        var (bus, log) = CreateBus(s =>
            s.AddEventHandler<TestEvent, RecordingHandler>());

        var tasks = Enumerable.Range(0, 50)
            .Select(i => bus.PublishAsync(new TestEvent($"event-{i}")).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        log.Entries.Should().HaveCount(50);
    }

    [Fact]
    public async Task PublishAsync_AsyncHandler_CompletesSuccessfully()
    {
        var (bus, log) = CreateBus(s =>
            s.AddEventHandler<TestEvent, SlowHandler>());

        await bus.PublishAsync(new TestEvent("async"));

        log.Entries.Should().ContainSingle()
            .Which.Should().Be("SlowHandler:async");
    }

    #endregion

    #region Test Setup

    private static (IEventBus Bus, InvocationLog Log) CreateBus(
        Action<IServiceCollection>? configure = null,
        Action<EventKitOptions>? options = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventKit(options);

        var log = new InvocationLog();
        services.AddSingleton(log);

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IEventBus>(), log);
    }

    #endregion
}
