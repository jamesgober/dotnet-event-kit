using JG.EventKit;
using JG.EventKit.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace JG.EventKit.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private sealed record TestEvent(string Value);

    private sealed class TestHandler : IEventHandler<TestEvent>
    {
        public ValueTask HandleAsync(TestEvent @event, CancellationToken cancellationToken) => default;
    }

    private sealed class TestMiddleware : IEventMiddleware
    {
        public ValueTask InvokeAsync<TEvent>(
            TEvent @event,
            EventDispatchDelegate next,
            CancellationToken cancellationToken) where TEvent : notnull
        {
            return next();
        }
    }

    [Fact]
    public void AddEventKit_RegistersEventBus()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventKit();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IEventBus>();

        bus.Should().NotBeNull();
    }

    [Fact]
    public void AddEventKit_CalledTwice_DoesNotDuplicate()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventKit();
        services.AddEventKit();

        var provider = services.BuildServiceProvider();
        var instances = provider.GetServices<IEventBus>();

        instances.Should().ContainSingle();
    }

    [Fact]
    public void AddEventKit_WithOptions_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEventKit(o =>
        {
            o.OnError = EventErrorPolicy.Aggregate;
            o.MaxParallelHandlers = 8;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventKitOptions>>();

        options.Value.OnError.Should().Be(EventErrorPolicy.Aggregate);
        options.Value.MaxParallelHandlers.Should().Be(8);
    }

    [Fact]
    public void AddEventHandler_RegistersSubscription()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventKit();

        services.AddEventHandler<TestEvent, TestHandler>(priority: 5);

        var provider = services.BuildServiceProvider();
        var subscriptions = provider.GetServices<EventSubscription>();

        subscriptions.Should().ContainSingle()
            .Which.Should().Match<EventSubscription>(s =>
                s.EventType == typeof(TestEvent) &&
                s.HandlerType == typeof(TestHandler) &&
                s.Priority == 5 &&
                !s.HasFilter);
    }

    [Fact]
    public void AddEventHandler_WithFilter_SetsHasFilter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventKit();

        services.AddEventHandler<TestEvent, TestHandler>(filter: e => e.Value == "yes");

        var provider = services.BuildServiceProvider();
        var subscription = provider.GetServices<EventSubscription>().Single();

        subscription.HasFilter.Should().BeTrue();
    }

    [Fact]
    public void AddEventHandler_DefaultLifetime_IsTransient()
    {
        var services = new ServiceCollection();
        services.AddEventHandler<TestEvent, TestHandler>();

        var descriptor = services.First(d => d.ServiceType == typeof(TestHandler));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddEventHandler_ScopedLifetime_RegistersAsScoped()
    {
        var services = new ServiceCollection();
        services.AddEventHandler<TestEvent, TestHandler>(lifetime: ServiceLifetime.Scoped);

        var descriptor = services.First(d => d.ServiceType == typeof(TestHandler));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddEventMiddleware_RegistersMiddleware()
    {
        var services = new ServiceCollection();
        services.AddEventMiddleware<TestMiddleware>();

        var descriptor = services.First(d => d.ServiceType == typeof(IEventMiddleware));

        descriptor.ImplementationType.Should().Be(typeof(TestMiddleware));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddEventKit_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddEventKit();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddEventHandler_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddEventHandler<TestEvent, TestHandler>();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddEventMiddleware_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddEventMiddleware<TestMiddleware>();

        act.Should().Throw<ArgumentNullException>();
    }
}
