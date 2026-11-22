using DeskBuddy.App.Diagnostics;

namespace DeskBuddy.App.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="StallTracker"/>.
/// </summary>
public sealed class StallTrackerTests
{
    /// <summary>
    /// The threshold the tests use.
    /// </summary>
    private static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// The time the tests start at.
    /// </summary>
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A second probe is not posted while the first one waits.
    /// </summary>
    [Fact]
    public void TryBeginProbe_WhileOneIsOutstanding_RefusesAnother()
    {
        // Arrange
        var tracker = new StallTracker(Threshold);
        tracker.TryBeginProbe(Start).Should().BeTrue();

        // Act
        var second = tracker.TryBeginProbe(Start.AddMilliseconds(100));

        // Assert
        second.Should().BeFalse();
    }

    /// <summary>
    /// A probe answered before the threshold is no stall, neither while waiting nor afterwards.
    /// </summary>
    [Fact]
    public void Answer_WithinThreshold_ReportsNothing()
    {
        // Arrange
        var tracker = new StallTracker(Threshold);
        tracker.TryBeginProbe(Start);
        tracker.CheckStalled(Start.AddMilliseconds(399)).Should().BeNull();

        // Act
        var waited = tracker.Answer(Start.AddMilliseconds(399));

        // Assert
        waited.Should().BeNull();
    }

    /// <summary>
    /// A probe that waits for the threshold is reported once, however often it is checked.
    /// </summary>
    [Fact]
    public void CheckStalled_PastThreshold_ReportsOnce()
    {
        // Arrange
        var tracker = new StallTracker(Threshold);
        tracker.TryBeginProbe(Start);

        // Act
        var first = tracker.CheckStalled(Start.AddMilliseconds(400));
        var second = tracker.CheckStalled(Start.AddMilliseconds(900));

        // Assert
        first.Should().Be(TimeSpan.FromMilliseconds(400));
        second.Should().BeNull();
    }

    /// <summary>
    /// The end of a reported stall says how long it lasted, and the next probe starts afresh.
    /// </summary>
    [Fact]
    public void Answer_AfterReportedStall_ReportsItsLengthAndResets()
    {
        // Arrange
        var tracker = new StallTracker(Threshold);
        tracker.TryBeginProbe(Start);
        tracker.CheckStalled(Start.AddMilliseconds(500));

        // Act
        var waited = tracker.Answer(Start.AddSeconds(3));

        // Assert
        waited.Should().Be(TimeSpan.FromSeconds(3));
        tracker.TryBeginProbe(Start.AddSeconds(3)).Should().BeTrue();
        tracker.CheckStalled(Start.AddSeconds(3).AddMilliseconds(100)).Should().BeNull();
    }

    /// <summary>
    /// Nothing is reported when no probe is outstanding.
    /// </summary>
    [Fact]
    public void CheckStalled_WithoutProbe_ReportsNothing()
    {
        // Arrange
        var tracker = new StallTracker(Threshold);

        // Act
        var waited = tracker.CheckStalled(Start.AddHours(1));

        // Assert
        waited.Should().BeNull();
    }
}
