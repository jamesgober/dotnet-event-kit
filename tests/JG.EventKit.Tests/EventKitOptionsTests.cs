using JG.EventKit;

namespace JG.EventKit.Tests;

public sealed class EventKitOptionsTests
{
    [Fact]
    public void MaxParallelHandlers_DefaultValue_IsOne()
    {
        var options = new EventKitOptions();

        options.MaxParallelHandlers.Should().Be(1);
    }

    [Fact]
    public void MaxParallelHandlers_ValidValue_Sets()
    {
        var options = new EventKitOptions { MaxParallelHandlers = 16 };

        options.MaxParallelHandlers.Should().Be(16);
    }

    [Fact]
    public void MaxParallelHandlers_Zero_ThrowsArgumentOutOfRange()
    {
        var options = new EventKitOptions();

        var act = () => options.MaxParallelHandlers = 0;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MaxParallelHandlers_Negative_ThrowsArgumentOutOfRange()
    {
        var options = new EventKitOptions();

        var act = () => options.MaxParallelHandlers = -1;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OnError_DefaultValue_IsLogAndContinue()
    {
        var options = new EventKitOptions();

        options.OnError.Should().Be(EventErrorPolicy.LogAndContinue);
    }

    [Fact]
    public void DeadLetterHandler_DefaultValue_IsNull()
    {
        var options = new EventKitOptions();

        options.DeadLetterHandler.Should().BeNull();
    }
}
