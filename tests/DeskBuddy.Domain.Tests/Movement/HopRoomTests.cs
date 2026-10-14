using AwesomeAssertions;
using DeskBuddy.Domain.Movement;

namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="HopRoom"/>.
/// </summary>
public class HopRoomTests
{
    /// <summary>
    /// The area used by the tests: x from 0 to 1000.
    /// </summary>
    private static readonly MovementBounds Bounds = new(0, 1000, 0, 800);

    /// <summary>
    /// The room needed is what the hop covers at its faster speed.
    /// </summary>
    [Fact]
    public void Required_WalkAndHop_IsTheHopLeap()
    {
        // Act
        var required = HopRoom.Required(walkSpeed: 36, hopSpeedFactor: 2, hopDuration: TimeSpan.FromSeconds(0.625));

        // Assert
        required.Should().BeApproximately(45, 1e-9);
    }

    /// <summary>
    /// Walking right with enough room before the right edge, there is room to hop.
    /// </summary>
    [Fact]
    public void IsClear_WalkingRightWithRoom_ReturnsTrue()
    {
        // Act
        var clear = HopRoom.IsClear(new Point2(500, 800), 1, Bounds, required: 45);

        // Assert
        clear.Should().BeTrue();
    }

    /// <summary>
    /// Walking right close to the right edge, there is no room to hop.
    /// </summary>
    [Fact]
    public void IsClear_WalkingRightNearTheRightEdge_ReturnsFalse()
    {
        // Act
        var clear = HopRoom.IsClear(new Point2(970, 800), 1, Bounds, required: 45);

        // Assert
        clear.Should().BeFalse();
    }

    /// <summary>
    /// Walking left close to the left edge, there is no room to hop.
    /// </summary>
    [Fact]
    public void IsClear_WalkingLeftNearTheLeftEdge_ReturnsFalse()
    {
        // Act
        var clear = HopRoom.IsClear(new Point2(30, 800), -1, Bounds, required: 45);

        // Assert
        clear.Should().BeFalse();
    }

    /// <summary>
    /// Only the edge the pet walks towards counts: close to the left edge but walking right, there is room.
    /// </summary>
    [Fact]
    public void IsClear_NearTheEdgeBehind_ReturnsTrue()
    {
        // Act
        var clear = HopRoom.IsClear(new Point2(20, 800), 1, Bounds, required: 45);

        // Assert
        clear.Should().BeTrue();
    }

    /// <summary>
    /// Room exactly as large as needed is enough.
    /// </summary>
    [Fact]
    public void IsClear_RoomEqualsRequired_ReturnsTrue()
    {
        // Act
        var clear = HopRoom.IsClear(new Point2(955, 800), 1, Bounds, required: 45);

        // Assert
        clear.Should().BeTrue();
    }
}
