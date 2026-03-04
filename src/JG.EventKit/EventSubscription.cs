namespace JG.EventKit;

/// <summary>
/// Describes a handler registration for a specific event type, including
/// execution priority and an optional filter predicate.
/// </summary>
public sealed class EventSubscription
{
    /// <summary>
    /// Initializes a new <see cref="EventSubscription"/>.
    /// </summary>
    internal EventSubscription(Type eventType, Type handlerType, int priority, Func<object, bool>? filter)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(handlerType);

        EventType = eventType;
        HandlerType = handlerType;
        Priority = priority;
        Filter = filter;
    }

    /// <summary>
    /// The event type this subscription targets.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// The handler implementation type resolved from the DI container.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Execution priority. Lower values execute first. Handlers with equal priority
    /// maintain their original registration order (stable sort).
    /// </summary>
        public int Priority { get; }

    /// <summary>
    /// Returns <c>true</c> if this subscription includes a filter predicate that
    /// can skip handler invocation for non-matching events.
    /// </summary>
    public bool HasFilter => Filter is not null;

    /// <summary>
    /// The filter predicate, or <c>null</c> if no filter is configured.
    /// </summary>
    internal Func<object, bool>? Filter { get; }
}
