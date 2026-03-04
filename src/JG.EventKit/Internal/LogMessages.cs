using Microsoft.Extensions.Logging;

namespace JG.EventKit.Internal;

/// <summary>
/// High-performance log messages using source-generated LoggerMessage delegates.
/// </summary>
internal static partial class LogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Event handler {HandlerType} failed while processing {EventType}.")]
    public static partial void HandlerFailed(
        this ILogger logger,
        Exception exception,
        string handlerType,
        string eventType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Dead letter handler failed for event {EventType}.")]
    public static partial void DeadLetterHandlerFailed(
        this ILogger logger,
        Exception exception,
        string eventType);
}
