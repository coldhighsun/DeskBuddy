using AwesomeAssertions;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="DragTracker"/> and <see cref="BubblePlacement"/>.
/// </summary>
public sealed class PixelGeometryTests
{
    /// <summary>
    /// The window follows the cursor with the grab offset kept.
    /// </summary>
    [Fact]
    public void Move_SameSize_KeepsTheGrabOffset()
    {
        // Arrange
        var tracker = new DragTracker();
        tracker.Begin(150, 250, new PixelRect(100, 200, 300, 400));

        // Act
        var (x, y) = tracker.Move(1000, 600, 200, 200);

        // Assert
        x.Should().Be(950);
        y.Should().Be(550);
    }

    /// <summary>
    /// When the window shrinks on a monitor with lower scaling, the grab point stays under the cursor.
    /// </summary>
    [Fact]
    public void Move_WindowShrinksOnAnotherMonitor_KeepsTheSameSpotUnderTheCursor()
    {
        // Arrange
        // Grabbed at the centre of a 200 px window; the window then becomes 100 px wide.
        var tracker = new DragTracker();
        tracker.Begin(200, 200, new PixelRect(100, 100, 300, 300));

        // Act
        var (x, y) = tracker.Move(2000, 500, 100, 100);

        // Assert
        x.Should().Be(1950);
        y.Should().Be(450);
    }

    /// <summary>
    /// An empty window does not divide by zero.
    /// </summary>
    [Fact]
    public void Begin_EmptyWindow_DoesNotThrow()
    {
        // Arrange
        var tracker = new DragTracker();

        // Act
        var act = () => tracker.Begin(10, 10, new PixelRect(5, 5, 5, 5));

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Pixels on a monitor left of the primary one are negative and still work.
    /// </summary>
    [Fact]
    public void Move_NegativeScreenCoordinates_Works()
    {
        // Arrange
        var tracker = new DragTracker();
        tracker.Begin(-1800, 300, new PixelRect(-1900, 200, -1700, 400));

        // Act
        var (x, y) = tracker.Move(-1000, 500, 200, 200);

        // Assert
        x.Should().Be(-1100);
        y.Should().Be(400);
    }

    /// <summary>
    /// The bubble is centred above the pet when there is room.
    /// </summary>
    [Fact]
    public void Compute_RoomOnBothSides_CentresAbovePet()
    {
        // Arrange
        var pet = new PixelRect(1000, 800, 1160, 960);
        var workArea = new PixelRect(0, 0, 1920, 1040);

        // Act
        var (left, top, centre) = BubblePlacement.Compute(pet, 300, 100, 35, workArea);

        // Assert
        left.Should().Be(930);
        top.Should().Be(735);
        centre.Should().Be(1080);
    }

    /// <summary>
    /// A bubble that would hang over the right edge is pushed back inside the work area.
    /// </summary>
    [Fact]
    public void Compute_PetAtRightEdge_KeepsBubbleInsideWorkArea()
    {
        // Arrange
        var pet = new PixelRect(1760, 800, 1920, 960);
        var workArea = new PixelRect(0, 0, 1920, 1040);

        // Act
        var (left, _, _) = BubblePlacement.Compute(pet, 300, 100, 35, workArea);

        // Assert
        left.Should().Be(1620);
    }

    /// <summary>
    /// On a monitor to the left of the primary one the work area has negative coordinates and the bubble stays in it.
    /// </summary>
    [Fact]
    public void Compute_SecondaryMonitorLeftOfPrimary_StaysOnThatMonitor()
    {
        // Arrange
        var pet = new PixelRect(-1920, 500, -1760, 660);
        var workArea = new PixelRect(-1920, 0, 0, 1040);

        // Act
        var (left, _, _) = BubblePlacement.Compute(pet, 300, 100, 35, workArea);

        // Assert
        left.Should().Be(-1920);
    }

    /// <summary>
    /// A bubble taller than the room above the pet is held at the top of the work area.
    /// </summary>
    [Fact]
    public void Compute_PetNearTop_BubbleStopsAtWorkAreaTop()
    {
        // Arrange
        var pet = new PixelRect(500, 20, 660, 180);
        var workArea = new PixelRect(0, 0, 1920, 1040);

        // Act
        var (_, top, _) = BubblePlacement.Compute(pet, 300, 200, 35, workArea);

        // Assert
        top.Should().Be(0);
    }

    /// <summary>
    /// The bubble ends a small gap above the sprite, however much transparent space the skin leaves over its head.
    /// </summary>
    [Theory]
    [InlineData(0.0, 4, -4)]
    [InlineData(0.25, 4, 36)]
    [InlineData(0.5, 0, 80)]
    public void TailOverlap_SpriteStartsLowerInWindow_ReachesDownToSprite(double spriteTop, int gap, int expected)
    {
        // Act
        var overlap = BubblePlacement.TailOverlap(160, spriteTop, gap);

        // Assert
        overlap.Should().Be(expected);
    }

    /// <summary>
    /// The bubble's bottom edge lands just above the top of the sprite.
    /// </summary>
    [Fact]
    public void Compute_OverlapFromSpriteTop_PlacesBubbleBottomAboveSprite()
    {
        // Arrange
        var pet = new PixelRect(1000, 800, 1160, 960);
        var workArea = new PixelRect(0, 0, 1920, 1040);
        var overlap = BubblePlacement.TailOverlap(pet.Height, 0.25, 4);

        // Act
        var (_, top, _) = BubblePlacement.Compute(pet, 300, 100, overlap, workArea);

        // Assert
        (top + 100).Should().Be(pet.Top + 40 - 4);
    }
}
