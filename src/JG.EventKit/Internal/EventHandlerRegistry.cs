using System.Collections.Concurrent;

namespace JG.EventKit.Internal;

/// <summary>
/// Maintains a pre-sorted, cached lookup of handler subscriptions keyed by event type.
/// Thread-safe for concurrent reads after construction.
/// </summary>
internal sealed class EventHandlerRegistry
{
    private readonly ConcurrentDictionary<Type, EventSubscription[]> _subscriptions;

    public EventHandlerRegistry(IEnumerable<EventSubscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        _subscriptions = new ConcurrentDictionary<Type, EventSubscription[]>(
            subscriptions
                .GroupBy(static s => s.EventType)
                .Select(static g => KeyValuePair.Create(
                    g.Key,
                    g.OrderBy(static s => s.Priority).ToArray())));
    }

    /// <summary>
    /// Returns the priority-sorted subscriptions for <paramref name="eventType"/>,
    /// or an empty array if no handlers are registered.
    /// </summary>
    public EventSubscription[] GetSubscriptions(Type eventType)
    {
        return _subscriptions.TryGetValue(eventType, out var subs) ? subs : [];
    }
}
