#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
#pragma warning disable CA1716 // Identifiers should not match keywords

namespace JG.EventKit;

/// <summary>
/// Publishes events to registered handlers within the current process.
/// </summary>
/// <remarks>
/// <para>
/// Inject this interface into any service that needs to raise events. Events are dispatched
/// to handlers in priority order, filtered by any registered predicates, and processed
/// according to the configured <see cref="EventErrorPolicy"/>.
/// </para>
/// <para>
/// All implementations are thread-safe and support concurrent publish calls from multiple threads.
/// </para>
/// </remarks>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all registered handlers for <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type. Must be a non-null reference or value type.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="cancellationToken">Token to cancel the publish operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when all handlers have finished.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="event"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="AggregateException">
    /// One or more handlers threw exceptions and the error policy is <see cref="EventErrorPolicy.Aggregate"/>.
    /// </exception>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
