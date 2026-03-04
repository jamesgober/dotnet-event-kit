using JG.EventKit;
using JG.EventKit.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering EventKit services in a <see cref="IServiceCollection"/>.
/// </summary>
public static class EventKitServiceCollectionExtensions
{
    /// <summary>
    /// Adds the EventKit event bus and its dependencies to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to configure <see cref="EventKitOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddEventKit(options =&gt;
    /// {
    ///     options.OnError = EventErrorPolicy.LogAndContinue;
    ///     options.MaxParallelHandlers = 4;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddEventKit(
        this IServiceCollection services,
        Action<EventKitOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<EventKitOptions>();
        }

        services.TryAddSingleton<EventHandlerRegistry>();
        services.TryAddSingleton<IEventBus, EventBus>();

        return services;
    }

    /// <summary>
    /// Registers an event handler for <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event type to handle.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="priority">
    /// Execution priority. Lower values execute first. Defaults to <c>0</c>.
    /// </param>
    /// <param name="filter">
    /// Optional predicate to filter events before handler invocation.
    /// When provided, the handler is only invoked if the predicate returns <c>true</c>.
    /// </param>
    /// <param name="lifetime">
    /// The DI lifetime for the handler. Defaults to <see cref="ServiceLifetime.Transient"/>.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddEventHandler&lt;UserRegistered, WelcomeEmailHandler&gt;(priority: 10);
    /// </code>
    /// </example>
    public static IServiceCollection AddEventHandler<TEvent, THandler>(
        this IServiceCollection services,
        int priority = 0,
        Func<TEvent, bool>? filter = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TEvent : notnull
        where THandler : class, IEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAdd(new ServiceDescriptor(typeof(THandler), typeof(THandler), lifetime));

        Func<object, bool>? wrappedFilter = filter is not null
            ? obj => filter((TEvent)obj)
            : null;

        services.AddSingleton(new EventSubscription(typeof(TEvent), typeof(THandler), priority, wrappedFilter));

        return services;
    }

    /// <summary>
    /// Registers an event middleware that wraps all event dispatch.
    /// Middleware executes in registration order.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
    public static IServiceCollection AddEventMiddleware<TMiddleware>(
        this IServiceCollection services)
        where TMiddleware : class, IEventMiddleware
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEventMiddleware, TMiddleware>();

        return services;
    }
}
