#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
#pragma warning disable CA1716 // Identifiers should not match keywords

namespace JG.EventKit;

/// <summary>
/// Delegate representing the next step in the event dispatch pipeline.
/// </summary>
/// <returns>A <see cref="ValueTask"/> that completes when the remaining pipeline has finished.</returns>
public delegate ValueTask EventDispatchDelegate();

/// <summary>
/// Middleware that wraps event dispatch for cross-cutting concerns such as
/// logging, timing, error handling, or retry logic.
/// </summary>
/// <remarks>
/// Middleware executes in registration order. Each middleware must call the <c>next</c> delegate
/// to continue the pipeline, or skip it to short-circuit dispatch.
/// Implementations are registered as singletons and must be thread-safe.
/// </remarks>
public interface IEventMiddleware
{
    /// <summary>
    /// Invoked for each event passing through the dispatch pipeline.
    /// </summary>
    /// <typeparam name="TEvent">The event type being dispatched.</typeparam>
    /// <param name="event">The event being dispatched.</param>
    /// <param name="next">Delegate to invoke the next middleware or the final handler dispatch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when this middleware step is finished.</returns>
    ValueTask InvokeAsync<TEvent>(TEvent @event, EventDispatchDelegate next, CancellationToken cancellationToken)
        where TEvent : notnull;
}
