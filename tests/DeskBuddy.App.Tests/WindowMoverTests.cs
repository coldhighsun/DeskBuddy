using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="WindowMover.TryGetMove"/>.
/// </summary>
public sealed class WindowMoverTests
{
    /// <summary>
    /// A step to another device pixel is a move, converted at the display scale.
    /// </summary>
    [Fact]
    public void TryGetMove_TargetInAnotherPixel_MovesInDevicePixels()
    {
        // Arrange
        // 150% scaling: 100 DIP is 150 px, and 102 DIP is 153 px.

        // Act
        var moved = WindowMover.TryGetMove(100, 50, 102, 50, 1.5, 1.5, out var x, out var y);

        // Assert
        moved.Should().BeTrue();
        x.Should().Be(153);
        y.Should().Be(75);
    }

    /// <summary>
    /// A step smaller than a pixel does not call the window manager.
    /// </summary>
    [Fact]
    public void TryGetMove_TargetInSamePixel_DoesNotMove()
    {
        // Arrange
        // At 100% scaling 100.2 and 100.4 DIP are both pixel 100.

        // Act
        var moved = WindowMover.TryGetMove(100.2, 50, 100.4, 50, 1, 1, out var x, out var y);

        // Assert
        moved.Should().BeFalse();
        x.Should().Be(100);
        y.Should().Be(50);
    }

    /// <summary>
    /// A change in only the vertical position still counts as a move.
    /// </summary>
    [Fact]
    public void TryGetMove_OnlyVerticalChange_Moves()
    {
        // Arrange and Act
        var moved = WindowMover.TryGetMove(100, 50, 100, 52, 1, 1, out _, out var y);

        // Assert
        moved.Should().BeTrue();
        y.Should().Be(52);
    }

    /// <summary>
    /// Positions on a monitor left of the primary one have negative coordinates and still convert correctly.
    /// </summary>
    [Fact]
    public void TryGetMove_NegativeCoordinates_RoundAwayFromZero()
    {
        // Arrange and Act
        var moved = WindowMover.TryGetMove(-100, 0, -100.5, 0, 1, 1, out var x, out _);

        // Assert
        moved.Should().BeTrue();
        x.Should().Be(-101);
    }
}
