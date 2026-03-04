namespace JG.EventKit;

/// <summary>
/// Wraps an event that was published but had no matching handlers.
/// Delivered to the <see cref="EventKitOptions.DeadLetterHandler"/> callback when configured.
/// </summary>
public sealed class DeadLetterEvent
{
    /// <summary>
    /// Initializes a new <see cref="DeadLetterEvent"/>.
    /// </summary>
    internal DeadLetterEvent(object @event, Type eventType)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(eventType);

        Event = @event;
        EventType = eventType;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// The original event instance that had no handlers.
    /// </summary>
    public object Event { get; }

    /// <summary>
    /// The runtime type of the published event.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// UTC timestamp when the dead letter was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}
