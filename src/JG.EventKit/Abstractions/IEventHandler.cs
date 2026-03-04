#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
#pragma warning disable CA1716 // Identifiers should not match keywords

namespace JG.EventKit;

/// <summary>
/// Handles events of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The event type this handler processes.</typeparam>
/// <remarks>
/// Implementations are resolved from the DI container for each publish operation.
/// Handler lifetime (transient, scoped, singleton) is determined by the service registration.
/// </remarks>
public interface IEventHandler<in TEvent> where TEvent : notnull
{
    /// <summary>
    /// Handles the published event.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">Token to cancel the handling operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when handling is finished.</returns>
    ValueTask HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
