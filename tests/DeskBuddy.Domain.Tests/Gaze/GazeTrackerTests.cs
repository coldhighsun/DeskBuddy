using DeskBuddy.Domain.Gaze;

namespace DeskBuddy.Domain.Tests.Gaze;

/// <summary>
/// Tests for <see cref="GazeTracker"/>.
/// </summary>
public class GazeTrackerTests
{
    /// <summary>
    /// The pet looks along the larger offset.
    /// </summary>
    [Theory]
    [InlineData(200, 10, GazeDirection.Right)]
    [InlineData(-200, 10, GazeDirection.Left)]
    [InlineData(10, -200, GazeDirection.Up)]
    [InlineData(10, 200, GazeDirection.Down)]
    public void Of_PointerFarAway_LooksTowardsIt(double dx, double dy, GazeDirection expected)
    {
        // Act
        var direction = GazeTracker.Of(dx, dy, 64);

        // Assert
        direction.Should().Be(expected);
    }

    /// <summary>
    /// A pointer right next to the pet is looked at head on.
    /// </summary>
    [Fact]
    public void Of_PointerInDeadZone_LooksAhead()
    {
        // Act
        var direction = GazeTracker.Of(30, -20, 64);

        // Assert
        direction.Should().Be(GazeDirection.None);
    }

    /// <summary>
    /// Left and right swap for a mirrored sprite; the others stay.
    /// </summary>
    [Theory]
    [InlineData(GazeDirection.Left, GazeDirection.Right)]
    [InlineData(GazeDirection.Right, GazeDirection.Left)]
    [InlineData(GazeDirection.Up, GazeDirection.Up)]
    [InlineData(GazeDirection.None, GazeDirection.None)]
    public void Mirror_SwapsLeftAndRight(GazeDirection direction, GazeDirection expected)
    {
        // Act
        var mirrored = GazeTracker.Mirror(direction);

        // Assert
        mirrored.Should().Be(expected);
    }
}
