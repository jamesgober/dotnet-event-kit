using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JG.EventKit.Internal;

/// <summary>
/// Default event bus implementation. Resolves handlers from DI, applies middleware,
/// and dispatches events according to the configured error policy.
/// </summary>
/// <remarks>
/// <para>
/// This type is registered as a singleton. It creates a new <see cref="IServiceScope"/>
/// per publish call to support scoped handler lifetimes.
/// </para>
/// <para>
/// Thread-safe. Multiple threads can call <see cref="PublishAsync{TEvent}"/> concurrently
/// without external synchronization.
/// </para>
/// </remarks>
internal sealed class EventBus : IEventBus
{
    private readonly EventHandlerRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EventKitOptions _options;
    private readonly IEventMiddleware[] _middleware;
    private readonly ILogger<EventBus> _logger;

    public EventBus(
        EventHandlerRegistry registry,
        IServiceScopeFactory scopeFactory,
        IOptions<EventKitOptions> options,
        IEnumerable<IEventMiddleware> middleware,
        ILogger<EventBus> logger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(middleware);
        ArgumentNullException.ThrowIfNull(logger);

        _registry = registry;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _middleware = middleware as IEventMiddleware[] ?? middleware.ToArray();
        _logger = logger;

        ValidateOptions(_options);
    }

    /// <inheritdoc />
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);
        cancellationToken.ThrowIfCancellationRequested();

        return _middleware.Length == 0
            ? DispatchAsync(@event, cancellationToken)
            : ExecuteWithMiddlewareAsync(@event, cancellationToken);
    }

    private ValueTask ExecuteWithMiddlewareAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : notnull
    {
        EventDispatchDelegate pipeline = () => DispatchAsync(@event, cancellationToken);

        for (int i = _middleware.Length - 1; i >= 0; i--)
        {
            var current = _middleware[i];
            var next = pipeline;
            pipeline = () => current.InvokeAsync(@event, next, cancellationToken);
        }

        return pipeline();
    }

    private ValueTask DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : notnull
    {
        var subscriptions = _registry.GetSubscriptions(typeof(TEvent));

        if (subscriptions.Length == 0)
        {
            return HandleDeadLetterAsync(@event, cancellationToken);
        }

        return _options.MaxParallelHandlers <= 1
            ? DispatchSequentialAsync(@event, subscriptions, cancellationToken)
            : DispatchParallelAsync(@event, subscriptions, cancellationToken);
    }

    private async ValueTask DispatchSequentialAsync<TEvent>(
        TEvent @event,
        EventSubscription[] subscriptions,
        CancellationToken cancellationToken)
        where TEvent : notnull
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        List<Exception>? errors = null;
        bool handlerInvoked = false;

        for (int i = 0; i < subscriptions.Length; i++)
        {
            var sub = subscriptions[i];

            if (sub.Filter is not null && !sub.Filter(@event))
            {
                continue;
            }

            handlerInvoked = true;

            try
            {
                var handler = (IEventHandler<TEvent>)scope.ServiceProvider.GetRequiredService(sub.HandlerType);
                await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                switch (_options.OnError)
                {
                    case EventErrorPolicy.StopOnFirst:
                        throw;

                    case EventErrorPolicy.LogAndContinue:
                        LogHandlerError(sub.HandlerType, typeof(TEvent), ex);
                        break;

                    case EventErrorPolicy.Aggregate:
                        errors ??= [];
                        errors.Add(ex);
                        break;
                }
            }
        }

        if (!handlerInvoked)
        {
            await HandleDeadLetterAsync(@event, cancellationToken).ConfigureAwait(false);
        }

        if (errors is { Count: > 0 })
        {
            throw new AggregateException(
                $"One or more handlers failed for event '{typeof(TEvent).Name}'.",
                errors);
        }
    }

    private async ValueTask DispatchParallelAsync<TEvent>(
        TEvent @event,
        EventSubscription[] subscriptions,
        CancellationToken cancellationToken)
        where TEvent : notnull
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        // Single pass: collect matching subscriptions into a right-sized array.
        // This avoids a List allocation and ensures task/subscription indices stay aligned.
        var filtered = new EventSubscription[subscriptions.Length];
        int count = 0;

        for (int i = 0; i < subscriptions.Length; i++)
        {
            var sub = subscriptions[i];
            if (sub.Filter is null || sub.Filter(@event))
            {
                filtered[count++] = sub;
            }
        }

        if (count == 0)
        {
            await HandleDeadLetterAsync(@event, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var semaphore = new SemaphoreSlim(_options.MaxParallelHandlers, _options.MaxParallelHandlers);

        CancellationTokenSource? linkedCts = _options.OnError == EventErrorPolicy.StopOnFirst
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        try
        {
            var effectiveCt = linkedCts?.Token ?? cancellationToken;
            var tasks = new Task[count];

            for (int i = 0; i < count; i++)
            {
                tasks[i] = ExecuteHandlerWithThrottleAsync(
                    scope.ServiceProvider, filtered[i], @event, semaphore, linkedCts, effectiveCt);
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                HandleParallelErrors(tasks, filtered, typeof(TEvent));
            }
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    private static async Task ExecuteHandlerWithThrottleAsync<TEvent>(
        IServiceProvider provider,
        EventSubscription subscription,
        TEvent @event,
        SemaphoreSlim semaphore,
        CancellationTokenSource? stopCts,
        CancellationToken cancellationToken)
        where TEvent : notnull
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var handler = (IEventHandler<TEvent>)provider.GetRequiredService(subscription.HandlerType);
            await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            if (stopCts is not null)
            {
                await stopCts.CancelAsync().ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private void HandleParallelErrors(Task[] tasks, EventSubscription[] subscriptions, Type eventType)
    {
        List<Exception>? aggregated = null;

        for (int i = 0; i < tasks.Length; i++)
        {
            if (!tasks[i].IsFaulted)
            {
                continue;
            }

            var taskException = tasks[i].Exception;
            if (taskException is null)
            {
                continue;
            }

            foreach (var inner in taskException.InnerExceptions)
            {
                if (inner is OperationCanceledException)
                {
                    continue;
                }

                switch (_options.OnError)
                {
                    case EventErrorPolicy.StopOnFirst:
                        ExceptionDispatchInfo.Throw(inner);
                        break;

                    case EventErrorPolicy.LogAndContinue:
                        LogHandlerError(subscriptions[i].HandlerType, eventType, inner);
                        break;

                    case EventErrorPolicy.Aggregate:
                        aggregated ??= [];
                        aggregated.Add(inner);
                        break;
                }
            }
        }

        if (aggregated is { Count: > 0 })
        {
            throw new AggregateException(
                $"One or more handlers failed for event '{eventType.Name}'.",
                aggregated);
        }
    }

    private async ValueTask HandleDeadLetterAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : notnull
    {
        if (_options.DeadLetterHandler is null)
        {
            return;
        }

        try
        {
            var deadLetter = new DeadLetterEvent(@event, typeof(TEvent));
            await _options.DeadLetterHandler(deadLetter, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.DeadLetterHandlerFailed(ex, typeof(TEvent).Name);
        }
    }

    private void LogHandlerError(Type handlerType, Type eventType, Exception exception)
    {
        _logger.HandlerFailed(exception, handlerType.Name, eventType.Name);
    }

    private static void ValidateOptions(EventKitOptions options)
    {
        if (!Enum.IsDefined(options.OnError))
        {
            throw new ArgumentException(
                $"Invalid {nameof(EventErrorPolicy)} value: {options.OnError}.",
                nameof(options));
        }
    }
}
