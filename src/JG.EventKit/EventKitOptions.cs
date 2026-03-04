namespace JG.EventKit;

/// <summary>
/// Configuration options for the event bus.
/// </summary>
public sealed class EventKitOptions
{
    private int _maxParallelHandlers = 1;

    /// <summary>
    /// Gets or sets the error handling policy for handler exceptions.
    /// Defaults to <see cref="EventErrorPolicy.LogAndContinue"/>.
    /// </summary>
    public EventErrorPolicy OnError { get; set; } = EventErrorPolicy.LogAndContinue;

    /// <summary>
    /// Gets or sets the maximum number of handlers that execute in parallel per publish.
    /// A value of <c>1</c> (the default) means sequential dispatch in priority order.
    /// Values greater than <c>1</c> enable parallel dispatch with the specified concurrency limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Value is less than 1.</exception>
    public int MaxParallelHandlers
    {
        get => _maxParallelHandlers;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxParallelHandlers = value;
        }
    }

    /// <summary>
    /// Gets or sets a callback invoked when an event is published but no handlers match.
    /// When <c>null</c> (the default), unhandled events are silently ignored.
    /// </summary>
    public Func<DeadLetterEvent, CancellationToken, ValueTask>? DeadLetterHandler { get; set; }
}
