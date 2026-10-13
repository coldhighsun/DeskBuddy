using AwesomeAssertions;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PetPlacement"/>.
/// </summary>
public class PetPlacementTests
{
    /// <summary>
    /// A first monitor at 100%: 1000 wide, 560 high.
    /// </summary>
    private static readonly MonitorArea First = new(new PixelRect(0, 0, 1000, 560), 1);

    /// <summary>
    /// A second monitor at 200%, to the right of the first.
    /// </summary>
    private static readonly MonitorArea Second = new(new PixelRect(1000, 100, 2600, 1000), 2);

    /// <summary>
    /// The pet always stands on the bottom edge of the work area, inside it and a margin away from its sides, whatever
    /// the chance gives, and the sizes are converted with the scaling of the monitor it lands on.
    /// </summary>
    [Fact]
    public void PickOnGround_ManyDraws_StaysOnBottomEdgeAwayFromSides()
    {
        // Arrange
        var random = new Random(7);

        // Act: a pet of 64 DIP with a margin of 16 DIP.
        var spots = Enumerable.Range(0, 300)
            .Select(_ => PetPlacement.PickOnGround([First, Second], 64, 64, 16, random))
            .ToList();

        // Assert: 64 and 16 pixels on the first monitor, 128 and 32 pixels on the second, and the scaling that goes with it.
        spots.Should().OnlyContain(spot =>
            (spot.Y == 496 && spot.X >= 16 && spot.X <= 920 && spot.Scale == 1)
            || (spot.Y == 872 && spot.X >= 1032 && spot.X <= 2440 && spot.Scale == 2));
    }

    /// <summary>
    /// Over many draws the pet appears on every monitor and at different places.
    /// </summary>
    [Fact]
    public void PickOnGround_ManyDraws_UsesEveryMonitorAndVariesPosition()
    {
        // Arrange
        var random = new Random(11);

        // Act
        var spots = Enumerable.Range(0, 300)
            .Select(_ => PetPlacement.PickOnGround([First, Second], 64, 64, 16, random))
            .ToList();

        // Assert
        spots.Should().Contain(spot => spot.X < 1000);
        spots.Should().Contain(spot => spot.X >= 1000);
        spots.Select(spot => spot.X).Distinct().Should().HaveCountGreaterThan(50);
    }

    /// <summary>
    /// A monitor with no room for the pet between the margins is passed over when another one has room.
    /// </summary>
    [Fact]
    public void PickOnGround_OneMonitorTooNarrow_UsesTheOtherOne()
    {
        // Arrange
        var narrow = new MonitorArea(new PixelRect(2000, 0, 2090, 300), 1);
        var random = new Random(3);

        // Act
        var spots = Enumerable.Range(0, 100)
            .Select(_ => PetPlacement.PickOnGround([narrow, First], 64, 64, 16, random))
            .ToList();

        // Assert
        spots.Should().OnlyContain(spot => spot.X < 1000);
    }

    /// <summary>
    /// When no monitor has room between the margins the pet is centered.
    /// </summary>
    [Fact]
    public void PickOnGround_NoRoomBetweenMargins_CentersPet()
    {
        // Arrange: at 200% the pet is 128 pixels wide and the margins together 64, in an area 150 wide.
        var monitor = new MonitorArea(new PixelRect(0, 0, 150, 300), 2);

        // Act
        var spot = PetPlacement.PickOnGround([monitor], 64, 64, 16, new Random(2));

        // Assert
        spot.Should().Be(new PetSpot(11, 172, 2));
    }

    /// <summary>
    /// An area smaller than the window gives the top-left corner of the area.
    /// </summary>
    [Fact]
    public void PickOnGround_AreaSmallerThanWindow_ReturnsAreaTopLeft()
    {
        // Act
        var spot = PetPlacement.PickOnGround([new MonitorArea(new PixelRect(10, 20, 60, 70), 1)], 64, 64, 16, new Random(5));

        // Assert
        spot.Should().Be(new PetSpot(10, 20, 1));
    }

    /// <summary>
    /// Left and Top for a spot are worked out with the scaling of the window, not that of the spot's monitor, because the
    /// window turns them into device pixels with its own.
    /// </summary>
    /// <param name="windowScale">
    /// The scaling of the window.
    /// </param>
    /// <param name="expectedLeft">
    /// The expected left edge in device-independent pixels.
    /// </param>
    /// <param name="expectedTop">
    /// The expected top edge in device-independent pixels.
    /// </param>
    [Theory]
    [InlineData(1, 300, 200)]
    [InlineData(1.5, 200, 400.0 / 3)]
    [InlineData(2, 150, 100)]
    public void PetSpot_WindowScale_GivesLeftAndTopForThatScale(double windowScale, double expectedLeft, double expectedTop)
    {
        // Arrange: a spot on a monitor at 200%, which must not matter.
        var spot = new PetSpot(300, 200, 2);

        // Act
        var left = spot.LeftFor(windowScale);
        var top = spot.TopFor(windowScale);

        // Assert
        left.Should().BeApproximately(expectedLeft, 0.0001);
        top.Should().BeApproximately(expectedTop, 0.0001);
    }

    /// <summary>
    /// A shown window is moved in device pixels in one step, and Left and Top are left alone.
    /// </summary>
    [Fact]
    public void Place_ShownWindow_MovesInDevicePixelsOnce()
    {
        // Arrange
        var target = new FakeTarget { IsVisible = true };

        // Act
        PetPlacement.Place(new PetSpot(300, 200, 2), target);

        // Assert
        target.Moves.Should().Equal((300, 200));
        target.LeftSet.Should().BeFalse();
        target.TopSet.Should().BeFalse();
    }

    /// <summary>
    /// A window that is not shown is placed through Left and Top, worked out with the window's scaling.
    /// </summary>
    [Fact]
    public void Place_HiddenWindow_SetsLeftAndTopWithTheWindowScale()
    {
        // Arrange
        var target = new FakeTarget { ScaleX = 1.5, ScaleY = 1.5 };

        // Act
        PetPlacement.Place(new PetSpot(300, 150, 2), target);

        // Assert
        target.Left.Should().BeApproximately(200, 0.0001);
        target.Top.Should().BeApproximately(100, 0.0001);
        target.Moves.Should().BeEmpty();
    }

    /// <summary>
    /// When setting Left takes the window to a monitor of another scaling, Top is worked out with the new scaling, not the
    /// one read before.
    /// </summary>
    [Fact]
    public void Place_HiddenWindowChangesScaleAfterLeft_TopUsesTheNewScale()
    {
        // Arrange: the window is at 100% and moves to a monitor at 200% as soon as its left edge is set.
        var target = new FakeTarget { ScaleX = 1, ScaleY = 1, ScaleAfterLeft = 2 };

        // Act
        PetPlacement.Place(new PetSpot(300, 200, 2), target);

        // Assert: the pixel row 200 at 200% is 100 device-independent pixels.
        target.Left.Should().BeApproximately(300, 0.0001);
        target.Top.Should().BeApproximately(100, 0.0001);
    }

    /// <summary>
    /// Placing without a window is a mistake of the caller.
    /// </summary>
    [Fact]
    public void Place_NoWindow_Throws()
    {
        // Act
        var act = () => PetPlacement.Place(new PetSpot(1, 1, 1), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// A window for tests that records how it is placed, and can change its scaling once its left edge is set, as a window
    /// does that moves to another monitor.
    /// </summary>
    private sealed class FakeTarget : IPlacementTarget
    {
        /// <summary>
        /// The left edge as last set.
        /// </summary>
        private double _left;

        /// <summary>
        /// The top edge as last set.
        /// </summary>
        private double _top;

        /// <inheritdoc />
        public bool IsVisible { get; init; }

        /// <inheritdoc />
        public double ScaleX { get; set; } = 1;

        /// <inheritdoc />
        public double ScaleY { get; set; } = 1;

        /// <summary>
        /// Gets or sets the scaling the window has after its left edge was set; zero for no change.
        /// </summary>
        public double ScaleAfterLeft { get; init; }

        /// <summary>
        /// Gets a value indicating whether the left edge was set.
        /// </summary>
        public bool LeftSet { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the top edge was set.
        /// </summary>
        public bool TopSet { get; private set; }

        /// <summary>
        /// Gets the moves in device pixels that were asked for.
        /// </summary>
        public List<(int X, int Y)> Moves { get; } = [];

        /// <inheritdoc />
        public double Left
        {
            get => _left;
            set
            {
                _left = value;
                LeftSet = true;
                if (ScaleAfterLeft > 0)
                {
                    ScaleX = ScaleAfterLeft;
                    ScaleY = ScaleAfterLeft;
                }
            }
        }

        /// <inheritdoc />
        public double Top
        {
            get => _top;
            set
            {
                _top = value;
                TopSet = true;
            }
        }

        /// <inheritdoc />
        public void MoveToPixels(int x, int y)
        {
            Moves.Add((x, y));
        }
    }

    /// <summary>
    /// Apetg for a spot without any monitor is a mistake of the caller.
    /// </summary>
    [Fact]
    public void PickOnGround_NoMonitors_Throws()
    {
        // Act
        var act = () => PetPlacement.PickOnGround([], 64, 64, 16, new Random(1));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
