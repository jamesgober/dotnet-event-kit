using System.Collections.Frozen;

namespace JG.EventKit.Internal;

/// <summary>
/// Maintains a pre-sorted, immutable lookup of handler subscriptions keyed by event type.
/// Thread-safe for concurrent reads. The internal dictionary is built once at construction
/// and never mutated.
/// </summary>
internal sealed class EventHandlerRegistry
{
    private static readonly EventSubscription[] Empty = [];

    private readonly FrozenDictionary<Type, EventSubscription[]> _subscriptions;

    public EventHandlerRegistry(IEnumerable<EventSubscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        _subscriptions = subscriptions
            .GroupBy(static s => s.EventType)
            .ToFrozenDictionary(
                static g => g.Key,
                static g => g.OrderBy(static s => s.Priority).ToArray());
    }

    /// <summary>
    /// Returns the priority-sorted subscriptions for <paramref name="eventType"/>,
    /// or a cached empty array if no handlers are registered.
    /// </summary>
    public EventSubscription[] GetSubscriptions(Type eventType)
    {
        return _subscriptions.TryGetValue(eventType, out var subs) ? subs : Empty;
    }
}
