namespace JG.EventKit;

/// <summary>
/// Controls how the event bus handles handler exceptions during dispatch.
/// </summary>
public enum EventErrorPolicy
{
    /// <summary>
    /// Stop dispatching immediately when the first handler throws.
    /// The exception propagates to the publisher.
    /// </summary>
    StopOnFirst = 0,

    /// <summary>
    /// Log the exception and continue dispatching to remaining handlers.
    /// No exception propagates to the publisher.
    /// </summary>
    LogAndContinue = 1,

    /// <summary>
    /// Execute all handlers and collect exceptions. If any handlers fail,
    /// throw an <see cref="AggregateException"/> containing all failures.
    /// </summary>
    Aggregate = 2
}
