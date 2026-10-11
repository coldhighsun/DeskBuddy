using AwesomeAssertions;
using DeskBuddy.Domain.Movement;

namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="EdgeSnap"/>.
/// </summary>
public class EdgeSnapTests
{
    /// <summary>
    /// The pet window: 64 wide and high.
    /// </summary>
    private static readonly Point2 Size = new(64, 64);

    /// <summary>
    /// A work area of 1000 by 600 for a 64 pixel window: the corner may go from 0 to 936 across and 0 to 536 down.
    /// </summary>
    private static readonly MovementBounds Area = new(0, 936, 0, 536);

    /// <summary>
    /// A pet dropped close to the left edge clings to it, facing as drawn.
    /// </summary>
    [Fact]
    public void Find_NearLeftEdge_SnapsLeft()
    {
        // Act
        var snap = EdgeSnap.Find(new Point2(5, 200), Size, Area, 8);

        // Assert
        snap.Should().Be(new EdgeSnapResult(new Point2(0, 200), 1));
    }

    /// <summary>
    /// A pet dropped close to the right edge clings to it, with the drawing mirrored.
    /// </summary>
    [Fact]
    public void Find_NearRightEdge_SnapsRightMirrored()
    {
        // Act
        var snap = EdgeSnap.Find(new Point2(932, 200), Size, Area, 8);

        // Assert
        snap.Should().Be(new EdgeSnapResult(new Point2(936, 200), -1));
    }

    /// <summary>
    /// A pet 24 pixels from the edge, as at the start, is not close enough with a snap distance of 8.
    /// </summary>
    [Fact]
    public void Find_AtStartingPlace_DoesNotSnap()
    {
        // Act
        var snap = EdgeSnap.Find(new Point2(936 - 24, 536), Size, Area, 8);

        // Assert
        snap.Should().BeNull();
    }

    /// <summary>
    /// An edge that is only the seam to another monitor does not count.
    /// </summary>
    [Fact]
    public void Find_EdgeWithMonitorBeside_DoesNotSnapThere()
    {
        // Act
        var left = EdgeSnap.Find(new Point2(5, 200), Size, Area, 8, allowLeft: false);
        var right = EdgeSnap.Find(new Point2(932, 200), Size, Area, 8, allowRight: false);
        var otherSide = EdgeSnap.Find(new Point2(932, 200), Size, Area, 8, allowLeft: false);

        // Assert
        left.Should().BeNull();
        right.Should().BeNull();
        otherSide.Should().NotBeNull();
    }

    /// <summary>
    /// A pet in the middle of the screen does not snap.
    /// </summary>
    [Fact]
    public void Find_Middle_DoesNotSnap()
    {
        // Act
        var snap = EdgeSnap.Find(new Point2(400, 300), Size, Area, 8);

        // Assert
        snap.Should().BeNull();
    }

    /// <summary>
    /// A pet dropped beyond the edge of the area still clings to it.
    /// </summary>
    [Fact]
    public void Find_BeyondLeftEdge_SnapsLeft()
    {
        // Act
        var snap = EdgeSnap.Find(new Point2(-20, 200), Size, Area, 8);

        // Assert
        snap!.Value.Position.X.Should().Be(0);
    }

    /// <summary>
    /// The height is kept inside the area.
    /// </summary>
    [Fact]
    public void Find_BelowTheArea_KeepsThePetInsideVertically()
    {
        // Act
        var snap = EdgeSnap.Find(new Point2(2, 900), Size, Area, 8);

        // Assert
        snap!.Value.Position.Y.Should().Be(536);
    }

    /// <summary>
    /// An area lower than the window does not throw; its top wins.
    /// </summary>
    [Fact]
    public void Find_AreaLowerThanWindow_TopWins()
    {
        // Arrange
        var tiny = new MovementBounds(0, 936, 10, -20);

        // Act
        var snap = EdgeSnap.Find(new Point2(2, 5), Size, tiny, 8);

        // Assert
        snap!.Value.Position.Y.Should().Be(10);
    }
}
