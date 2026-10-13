using AwesomeAssertions;
using DeskBuddy.App.Presentation;
using DeskBuddy.Domain;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="WalkRange"/> and <see cref="SeamGuard"/>.
/// </summary>
public class WalkRangeTests
{
    /// <summary>
    /// A first monitor, 1000 wide and 600 high, with a taskbar of 40 at the bottom.
    /// </summary>
    private static readonly MonitorArea Left = new(new PixelRect(0, 0, 1000, 560), 1, new PixelRect(0, 0, 1000, 600));

    /// <summary>
    /// A second monitor right of the first, 1200 wide and 600 high, with a taskbar of 60 at the bottom.
    /// </summary>
    private static readonly MonitorArea Middle = new(new PixelRect(1000, 0, 2200, 540), 1.5, new PixelRect(1000, 0, 2200, 600));

    /// <summary>
    /// The height of the pet window in device pixels.
    /// </summary>
    private const int PetHeight = 64;

    /// <summary>
    /// A third monitor right of the second.
    /// </summary>
    private static readonly MonitorArea Right = new(new PixelRect(2200, 0, 3000, 560), 1, new PixelRect(2200, 0, 3000, 600));

    /// <summary>
    /// With a single monitor the range is its work area and there is no seam.
    /// </summary>
    [Fact]
    public void Compute_SingleMonitor_IsItsWorkArea()
    {
        // Arrange & Act
        var range = WalkRange.Compute([Left], 500, 300, PetHeight);

        // Assert
        range.Should().NotBeNull();
        range!.Value.Left.Should().Be(0);
        range.Value.Right.Should().Be(1000);
        range.Value.Bottom.Should().Be(560);
        range.Value.Seams.Should().BeEmpty();
    }

    /// <summary>
    /// Without any monitor there is no range.
    /// </summary>
    [Fact]
    public void Compute_NoMonitors_ReturnsNull()
    {
        // Arrange & Act
        var range = WalkRange.Compute([], 0, 0, PetHeight);

        // Assert
        range.Should().BeNull();
    }

    /// <summary>
    /// Standing on either of two monitors that lie side by side, the range covers both and the seam is where they meet.
    /// </summary>
    /// <param name="centerX">
    /// The horizontal centre of the pet.
    /// </param>
    [Theory]
    [InlineData(500)]
    [InlineData(1500)]
    public void Compute_TwoMonitorsSideBySide_CoversBoth(double centerX)
    {
        // Arrange & Act
        var range = WalkRange.Compute([Left, Middle], centerX, 300, PetHeight);

        // Assert
        range!.Value.Left.Should().Be(0);
        range.Value.Right.Should().Be(2200);
        range.Value.Seams.Should().Equal(1000);
    }

    /// <summary>
    /// Standing on the middle one of three monitors in a row, the range covers all three and has two seams.
    /// </summary>
    [Fact]
    public void Compute_ThreeInARow_CoversAllAndTakesTaskbarOfCurrent()
    {
        // Arrange & Act
        var range = WalkRange.Compute([Right, Left, Middle], 1500, 400, PetHeight);

        // Assert
        range!.Value.Left.Should().Be(0);
        range.Value.Right.Should().Be(3000);
        range.Value.Seams.Should().Equal(1000, 2200);
        range.Value.Top.Should().Be(0);
        range.Value.Bottom.Should().Be(540);
        range.Value.Current.Should().Be(new PixelRect(1000, 0, 2200, 600));
        range.Value.RightScale.Should().Be(1);
    }

    /// <summary>
    /// Monitors with a gap between them are not walked across.
    /// </summary>
    [Fact]
    public void Compute_GapBetweenMonitors_StaysOnCurrent()
    {
        // Arrange
        var apart = new MonitorArea(new PixelRect(1100, 0, 2000, 560), 1, new PixelRect(1100, 0, 2000, 600));

        // Act
        var range = WalkRange.Compute([Left, apart], 500, 300, PetHeight);

        // Assert
        range!.Value.Right.Should().Be(1000);
        range.Value.Seams.Should().BeEmpty();
    }

    /// <summary>
    /// Monitors that touch at a corner only, with no height in common, are not walked across.
    /// </summary>
    [Fact]
    public void Compute_MonitorsWithoutCommonHeight_StaysOnCurrent()
    {
        // Arrange
        var below = new MonitorArea(new PixelRect(1000, 600, 2000, 1100), 1, new PixelRect(1000, 600, 2000, 1100));

        // Act
        var range = WalkRange.Compute([Left, below], 500, 300, PetHeight);

        // Assert
        range!.Value.Right.Should().Be(1000);
        range.Value.Seams.Should().BeEmpty();
    }

    /// <summary>
    /// Monitors lie side by side when their whole areas touch, even if a taskbar at the side moves the work areas apart.
    /// </summary>
    [Fact]
    public void Compute_TaskbarAtTheSeam_StillSideBySide()
    {
        // Arrange: the second monitor has its taskbar at the left, so its work area starts 48 further right.
        var withSideTaskbar = new MonitorArea(new PixelRect(1048, 0, 2000, 600), 1, new PixelRect(1000, 0, 2000, 600));

        // Act
        var range = WalkRange.Compute([Left, withSideTaskbar], 500, 300, PetHeight);

        // Assert
        range!.Value.Right.Should().Be(2000);
        range.Value.Seams.Should().Equal(1000);
    }

    /// <summary>
    /// Two monitors of different height are one row from either of them: the pet can walk from the taller one back onto
    /// the shorter one as well as the other way round.
    /// </summary>
    /// <param name="centerX">
    /// The horizontal centre of the pet.
    /// </param>
    [Theory]
    [InlineData(500)]
    [InlineData(1500)]
    public void Compute_MonitorsOfDifferentHeight_AreOneRowFromEitherSide(double centerX)
    {
        // Arrange: the right monitor reaches lower than the left one, so its taskbar line is lower.
        var taller = new MonitorArea(new PixelRect(1000, 0, 2000, 960), 1.25, new PixelRect(1000, 0, 2000, 1000));

        // Act
        var range = WalkRange.Compute([Left, taller], centerX, 300, PetHeight);

        // Assert
        range!.Value.Left.Should().Be(0);
        range.Value.Right.Should().Be(2000);
        range.Value.Seams.Should().Equal(1000);
        range.Value.WorkAreas.Should().HaveCount(2);
    }

    /// <summary>
    /// A monitor that has less than the height of the pet in common with the one beside it is too short to show the pet,
    /// so the pet does not walk onto it.
    /// </summary>
    [Fact]
    public void Compute_NeighborSharesLessThanPetHeight_StaysOnCurrent()
    {
        // Arrange: shares 20 pixels of height with the left monitor.
        var barely = new MonitorArea(new PixelRect(1000, 580, 2000, 1000), 1, new PixelRect(1000, 580, 2000, 1000));

        // Act
        var range = WalkRange.Compute([Left, barely], 500, 300, PetHeight);

        // Assert
        range!.Value.Right.Should().Be(1000);
        range.Value.Seams.Should().BeEmpty();
    }

    /// <summary>
    /// A pet over two monitors with taskbar lines at different heights keeps to what is on screen on both: above the
    /// higher taskbar line and below the lower top edge. Over one monitor it takes that monitor's.
    /// </summary>
    /// <param name="left">
    /// The window's left edge.
    /// </param>
    /// <param name="right">
    /// The window's right edge.
    /// </param>
    /// <param name="expectedTop">
    /// The expected top edge.
    /// </param>
    /// <param name="expectedBottom">
    /// The expected taskbar line.
    /// </param>
    [Theory]
    [InlineData(800, 900, 0, 560)]
    [InlineData(1200, 1300, 100, 960)]
    [InlineData(950, 1050, 100, 560)]
    [InlineData(900, 1000, 0, 560)]
    public void VerticalAt_WindowOverMonitors_KeepsToWhatIsOnAll(int left, int right, int expectedTop, int expectedBottom)
    {
        // Arrange
        var areas = new[] { new PixelRect(0, 0, 1000, 560), new PixelRect(1000, 100, 2000, 960) };

        // Act
        var vertical = WalkRange.VerticalAt(areas, left, right, (-1, -1));

        // Assert
        vertical.Should().Be((expectedTop, expectedBottom));
    }

    /// <summary>
    /// A monitor with a larger scaling shows the pet higher in pixels, so it has to share enough height for that: a
    /// pet of 64 pixels at 100% is 128 at 200%, which does not fit in 100 shared pixels.
    /// </summary>
    [Fact]
    public void Compute_NeighborWithLargerScalingSharesTooLittleForTallerPet_StaysOnCurrent()
    {
        // Arrange: shares 100 pixels of height (500..600) with the left monitor.
        var sharp = new MonitorArea(new PixelRect(1000, 500, 2000, 1500), 2, new PixelRect(1000, 500, 2000, 1500));

        // Act
        var range = WalkRange.Compute([Left, sharp], 500, 300, PetHeight);

        // Assert
        range!.Value.Right.Should().Be(1000);
    }

    /// <summary>
    /// The top edge and taskbar line are given in device-independent pixels of the present scaling.
    /// </summary>
    [Fact]
    public void VerticalAtDip_WindowOverBothMonitors_ConvertsWithScaling()
    {
        // Arrange
        var areas = new[] { new PixelRect(0, 0, 1000, 560), new PixelRect(1000, 100, 2000, 960) };

        // Act
        var vertical = WalkRange.VerticalAtDip(areas, 950, 1050, 1.25, (-1, -1));

        // Assert
        vertical.Top.Should().Be(80);
        vertical.Bottom.Should().Be(448);
    }

    /// <summary>
    /// A window over none of the work areas gets the fallback, which is already in device-independent pixels.
    /// </summary>
    [Fact]
    public void VerticalAtDip_WindowOverNothing_ReturnsFallbackUnchanged()
    {
        // Arrange & Act
        var vertical = WalkRange.VerticalAtDip([new PixelRect(0, 0, 1000, 560)], 1500, 1600, 1.25, (7.5, 9.5));

        // Assert
        vertical.Should().Be((7.5, 9.5));
    }

    /// <summary>
    /// A window over none of the work areas gets the fallback.
    /// </summary>
    [Fact]
    public void VerticalAt_WindowOverNothing_ReturnsFallback()
    {
        // Arrange & Act
        var vertical = WalkRange.VerticalAt([new PixelRect(0, 0, 1000, 560)], 1500, 1600, (7, 9));

        // Assert
        vertical.Should().Be((7, 9));
    }

    /// <summary>
    /// A pet whose centre is on the seam is on the monitor to the right of it, and one pixel before that on the left one.
    /// </summary>
    /// <param name="centerX">
    /// The horizontal centre of the pet.
    /// </param>
    /// <param name="expectedLeft">
    /// The left edge of the monitor it is expected to be on.
    /// </param>
    [Theory]
    [InlineData(999, 0)]
    [InlineData(1000, 1000)]
    public void Compute_CenterAtSeam_PicksMonitorToTheRight(double centerX, int expectedLeft)
    {
        // Arrange & Act
        var range = WalkRange.Compute([Left, Middle], centerX, 300, PetHeight);

        // Assert
        range!.Value.Current.Left.Should().Be(expectedLeft);
    }

    /// <summary>
    /// The range tells the scaling of the monitor at its right end, whatever monitor the pet is on.
    /// </summary>
    [Fact]
    public void Compute_RightMostMonitorHasOtherScale_ReportsItsScale()
    {
        // Arrange
        var sharp = new MonitorArea(new PixelRect(1000, 0, 2000, 560), 2, new PixelRect(1000, 0, 2000, 600));

        // Act
        var range = WalkRange.Compute([Left, sharp], 500, 300, PetHeight);

        // Assert
        range!.Value.RightScale.Should().Be(2);
    }

    /// <summary>
    /// A pet window that lies on one monitor, or only touches the seam, is not across it.
    /// </summary>
    /// <param name="left">
    /// The window's left edge.
    /// </param>
    /// <param name="right">
    /// The window's right edge.
    /// </param>
    [Theory]
    [InlineData(800, 900)]
    [InlineData(900, 1000)]
    [InlineData(1000, 1100)]
    public void Straddles_WindowNotAcrossSeam_ReturnsFalse(int left, int right)
    {
        // Arrange & Act
        var straddles = SeamGuard.Straddles(left, right, [1000]);

        // Assert
        straddles.Should().BeFalse();
    }

    /// <summary>
    /// A pet window with a part on each side of a seam is across it.
    /// </summary>
    [Fact]
    public void Straddles_WindowAcrossSeam_ReturnsTrue()
    {
        // Arrange & Act
        var straddles = SeamGuard.Straddles(950, 1050, [1000]);

        // Assert
        straddles.Should().BeTrue();
    }

    /// <summary>
    /// A walking pet on a seam holds back every change of state except being picked up and falling; anywhere else
    /// nothing is held back.
    /// </summary>
    /// <param name="shown">
    /// The state shown now.
    /// </param>
    /// <param name="target">
    /// The state to change to.
    /// </param>
    /// <param name="onSeam">
    /// Whether the pet is on a seam.
    /// </param>
    /// <param name="expected">
    /// Whether the change waits.
    /// </param>
    [Theory]
    [InlineData(PetState.Walk, PetState.Idle, true, true)]
    [InlineData(PetState.Walk, PetState.Happy, true, true)]
    [InlineData(PetState.Walk, PetState.Climb, true, true)]
    [InlineData(PetState.Walk, PetState.Drag, true, false)]
    [InlineData(PetState.Walk, PetState.Fall, true, false)]
    [InlineData(PetState.Walk, PetState.Glide, true, false)]
    [InlineData(PetState.Walk, PetState.Idle, false, false)]
    [InlineData(PetState.Idle, PetState.Happy, true, false)]
    public void HoldsStateChange_Case_ReturnsExpected(PetState shown, PetState target, bool onSeam, bool expected)
    {
        // Arrange & Act
        var holds = SeamGuard.HoldsStateChange(shown, target, onSeam);

        // Assert
        holds.Should().Be(expected);
    }

    /// <summary>
    /// A window that is not across a seam stays where it is.
    /// </summary>
    [Fact]
    public void NudgeOffSeam_NotAcrossSeam_KeepsPosition()
    {
        // Arrange & Act
        var left = SeamGuard.NudgeOffSeam(800, 900, [1000]);

        // Assert
        left.Should().Be(800);
    }

    /// <summary>
    /// A window across a seam goes to the side where more of it is.
    /// </summary>
    /// <param name="left">
    /// The window's left edge, 100 wide.
    /// </param>
    /// <param name="expected">
    /// The expected left edge.
    /// </param>
    [Theory]
    [InlineData(920, 900)]
    [InlineData(980, 1000)]
    [InlineData(950, 900)]
    public void NudgeOffSeam_AcrossSeam_GoesToLargerSide(int left, int expected)
    {
        // Arrange & Act
        var nudged = SeamGuard.NudgeOffSeam(left, left + 100, [1000]);

        // Assert
        nudged.Should().Be(expected);
    }
}
