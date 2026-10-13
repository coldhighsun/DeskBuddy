using AwesomeAssertions;
using DeskBuddy.App.Presentation;
using DeskBuddy.Domain.Movement;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="StepTracker"/>.
/// </summary>
public class StepTrackerTests
{
    /// <summary>
    /// A window of 100 by 80 pixels with its top-left corner at the given pixels.
    /// </summary>
    /// <param name="x">
    /// The left edge.
    /// </param>
    /// <param name="y">
    /// The top edge.
    /// </param>
    /// <returns>
    /// The bounds.
    /// </returns>
    private static PixelRect WindowAt(int x, int y)
    {
        return new PixelRect(x, y, x + 100, y + 80);
    }

    /// <summary>
    /// Positions become the nearest whole device pixels with the scaling of the monitor.
    /// </summary>
    [Fact]
    public void ToPixels_FractionalPosition_RoundsWithScaling()
    {
        // Arrange & Act
        var pixels = StepTracker.ToPixels(new Point2(100.4, 50.5), 1.25, 2);

        // Assert
        pixels.Should().Be((126, 101));
    }

    /// <summary>
    /// Before any step the position is the real pixels divided by the scaling.
    /// </summary>
    [Fact]
    public void Resolve_NothingRemembered_DividesRealPixelsByScaling()
    {
        // Arrange
        var tracker = new StepTracker();

        // Act
        var position = tracker.Resolve(WindowAt(250, 100), 1.25, 1.25);

        // Assert
        position.Should().Be(new Point2(200, 80));
    }

    /// <summary>
    /// A window still at the pixels the last step put it at gives the exact position of that step, so steps of less than
    /// a pixel add up instead of being lost.
    /// </summary>
    [Fact]
    public void Resolve_WindowWhereLastStepPutIt_ReturnsExactPosition()
    {
        // Arrange
        var tracker = new StepTracker();
        tracker.Remember((100, 50), new Point2(100.3, 50), 1, 1);

        // Act
        var position = tracker.Resolve(WindowAt(100, 50), 1, 1);

        // Assert
        position.Should().Be(new Point2(100.3, 50));
    }

    /// <summary>
    /// A window that something else has moved is taken from its real pixels.
    /// </summary>
    [Fact]
    public void Resolve_WindowMovedElsewhere_UsesRealPixels()
    {
        // Arrange
        var tracker = new StepTracker();
        tracker.Remember((100, 50), new Point2(100.3, 50), 1, 1);

        // Act
        var position = tracker.Resolve(WindowAt(400, 50), 1, 1);

        // Assert
        position.Should().Be(new Point2(400, 50));
    }

    /// <summary>
    /// When the scaling is not what it was, the remembered position (in the old scaling) is not used even if the window
    /// is on the same pixels.
    /// </summary>
    [Fact]
    public void Resolve_ScalingChanged_UsesRealPixelsWithNewScaling()
    {
        // Arrange
        var tracker = new StepTracker();
        tracker.Remember((250, 100), new Point2(250, 100), 1, 1);

        // Act
        var position = tracker.Resolve(WindowAt(250, 100), 1.25, 1.25);

        // Assert
        position.Should().Be(new Point2(200, 80));
    }
}
