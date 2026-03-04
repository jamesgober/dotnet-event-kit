using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using JG.EventKit;
using Microsoft.Extensions.DependencyInjection;

namespace JG.EventKit.Benchmarks;

/// <summary>
/// Benchmarks for the core event dispatch hot path.
/// Run with: dotnet run --project tests/JG.EventKit.Benchmarks -c Release
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class PublishBenchmarks
{
    private IEventBus _busNoMiddleware = null!;
    private IEventBus _busWithMiddleware = null!;
    private IEventBus _busThreeHandlers = null!;
    private IEventBus _busParallel = null!;
    private IEventBus _busWithFilter = null!;
    private IEventBus _busNoHandlers = null!;

    private readonly SampleEvent _event = new("benchmark", 42);

    [GlobalSetup]
    public void Setup()
    {
        _busNoMiddleware = BuildBus(s =>
        {
            s.AddEventHandler<SampleEvent, NoOpHandler>();
        });

        _busWithMiddleware = BuildBus(s =>
        {
            s.AddEventHandler<SampleEvent, NoOpHandler>();
            s.AddSingleton<IEventMiddleware, PassthroughMiddleware>();
        });

        _busThreeHandlers = BuildBus(s =>
        {
            s.AddEventHandler<SampleEvent, NoOpHandler>(priority: 0);
            s.AddEventHandler<SampleEvent, NoOpHandlerB>(priority: 10);
            s.AddEventHandler<SampleEvent, NoOpHandlerC>(priority: 20);
        });

        _busParallel = BuildBus(s =>
        {
            s.AddEventHandler<SampleEvent, NoOpHandler>(priority: 0);
            s.AddEventHandler<SampleEvent, NoOpHandlerB>(priority: 10);
            s.AddEventHandler<SampleEvent, NoOpHandlerC>(priority: 20);
        }, o => o.MaxParallelHandlers = 3);

        _busWithFilter = BuildBus(s =>
        {
            s.AddEventHandler<SampleEvent, NoOpHandler>(filter: e => e.Value > 10);
            s.AddEventHandler<SampleEvent, NoOpHandlerB>(filter: e => e.Value > 100);
        });

        _busNoHandlers = BuildBus(_ => { });
    }

    [Benchmark(Baseline = true)]
    public ValueTask SingleHandler()
        => _busNoMiddleware.PublishAsync(_event);

    [Benchmark]
    public ValueTask SingleHandler_WithMiddleware()
        => _busWithMiddleware.PublishAsync(_event);

    [Benchmark]
    public ValueTask ThreeHandlers_Sequential()
        => _busThreeHandlers.PublishAsync(_event);

    [Benchmark]
    public ValueTask ThreeHandlers_Parallel()
        => _busParallel.PublishAsync(_event);

    [Benchmark]
    public ValueTask TwoHandlers_WithFilter()
        => _busWithFilter.PublishAsync(_event);

    [Benchmark]
    public ValueTask NoHandlers_DeadLetter()
        => _busNoHandlers.PublishAsync(_event);

    #region Setup Helpers

    private static IEventBus BuildBus(
        Action<IServiceCollection> configure,
        Action<EventKitOptions>? options = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventKit(options);
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<IEventBus>();
    }

    public sealed record SampleEvent(string Name, int Value);

    public sealed class NoOpHandler : IEventHandler<SampleEvent>
    {
        public ValueTask HandleAsync(SampleEvent @event, CancellationToken cancellationToken)
            => default;
    }

    public sealed class NoOpHandlerB : IEventHandler<SampleEvent>
    {
        public ValueTask HandleAsync(SampleEvent @event, CancellationToken cancellationToken)
            => default;
    }

    public sealed class NoOpHandlerC : IEventHandler<SampleEvent>
    {
        public ValueTask HandleAsync(SampleEvent @event, CancellationToken cancellationToken)
            => default;
    }

    public sealed class PassthroughMiddleware : IEventMiddleware
    {
        public ValueTask InvokeAsync<TEvent>(
            TEvent @event, EventDispatchDelegate next, CancellationToken cancellationToken)
            where TEvent : notnull
            => next();
    }

    #endregion
}
