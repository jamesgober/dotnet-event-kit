using JG.EventKit;
using JG.EventKit.Internal;

namespace JG.EventKit.Tests;

public sealed class EventHandlerRegistryTests
{
    private sealed record EventA(string Value);
    private sealed record EventB(int Id);

    private sealed class HandlerA : IEventHandler<EventA>
    {
        public ValueTask HandleAsync(EventA @event, CancellationToken cancellationToken) => default;
    }

    private sealed class HandlerB : IEventHandler<EventB>
    {
        public ValueTask HandleAsync(EventB @event, CancellationToken cancellationToken) => default;
    }

    private sealed class AnotherHandlerA : IEventHandler<EventA>
    {
        public ValueTask HandleAsync(EventA @event, CancellationToken cancellationToken) => default;
    }

    [Fact]
    public void GetSubscriptions_RegisteredEventType_ReturnsSortedByPriority()
    {
        var subscriptions = new[]
        {
            new EventSubscription(typeof(EventA), typeof(HandlerA), priority: 20, filter: null),
            new EventSubscription(typeof(EventA), typeof(AnotherHandlerA), priority: 5, filter: null),
        };

        var registry = new EventHandlerRegistry(subscriptions);

        var result = registry.GetSubscriptions(typeof(EventA));

        result.Should().HaveCount(2);
        result[0].HandlerType.Should().Be(typeof(AnotherHandlerA));
        result[1].HandlerType.Should().Be(typeof(HandlerA));
    }

    [Fact]
    public void GetSubscriptions_UnregisteredEventType_ReturnsEmpty()
    {
        var subscriptions = new[]
        {
            new EventSubscription(typeof(EventA), typeof(HandlerA), priority: 0, filter: null),
        };

        var registry = new EventHandlerRegistry(subscriptions);

        var result = registry.GetSubscriptions(typeof(EventB));

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSubscriptions_NoSubscriptions_ReturnsEmpty()
    {
        var registry = new EventHandlerRegistry([]);

        var result = registry.GetSubscriptions(typeof(EventA));

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSubscriptions_MultipleEventTypes_ReturnsCorrectSubscriptions()
    {
        var subscriptions = new[]
        {
            new EventSubscription(typeof(EventA), typeof(HandlerA), priority: 0, filter: null),
            new EventSubscription(typeof(EventB), typeof(HandlerB), priority: 0, filter: null),
            new EventSubscription(typeof(EventA), typeof(AnotherHandlerA), priority: 10, filter: null),
        };

        var registry = new EventHandlerRegistry(subscriptions);

        registry.GetSubscriptions(typeof(EventA)).Should().HaveCount(2);
        registry.GetSubscriptions(typeof(EventB)).Should().HaveCount(1);
    }
}
