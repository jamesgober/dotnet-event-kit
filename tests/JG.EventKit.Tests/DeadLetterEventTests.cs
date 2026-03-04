using JG.EventKit;

namespace JG.EventKit.Tests;

public sealed class DeadLetterEventTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var sourceEvent = new object();
        var eventType = typeof(string);

        var before = DateTimeOffset.UtcNow;
        var deadLetter = CreateDeadLetterEvent(sourceEvent, eventType);
        var after = DateTimeOffset.UtcNow;

        deadLetter.Event.Should().BeSameAs(sourceEvent);
        deadLetter.EventType.Should().Be(eventType);
        deadLetter.Timestamp.Should().BeOnOrAfter(before);
        deadLetter.Timestamp.Should().BeOnOrBefore(after);
    }

    /// <summary>
    /// Uses reflection to invoke the internal constructor for isolated unit testing.
    /// </summary>
    private static DeadLetterEvent CreateDeadLetterEvent(object @event, Type eventType)
    {
        var ctor = typeof(DeadLetterEvent).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return (DeadLetterEvent)ctor[0].Invoke([@event, eventType]);
    }
}
