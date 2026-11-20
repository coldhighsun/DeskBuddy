using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Tests.Presentation.Placement;

/// <summary>
/// Tests for <see cref="CanopyPlacement"/> and <see cref="CanopyRig"/>.
/// </summary>
public sealed class CanopyPlacementTests
{
    /// <summary>
    /// A pet window of 128 x 128 pixels at (100, 200) on a display without scaling.
    /// </summary>
    private static readonly PixelRect Pet128 = new(100, 200, 228, 328);

    /// <summary>
    /// A pet that holds on with two paws beside its head, as drawn facing the way its frames face.
    /// </summary>
    private static readonly FramePoint[] TwoPaws = [new(0.2, 0.45), new(0.8, 0.5)];

    /// <summary>
    /// The canopy is one and a half times as wide as the pet; held at the default point it is centered over the pet,
    /// and that point is in the middle of the window, as far down the pet as the default says.
    /// </summary>
    [Fact]
    public void For_DefaultGrip_CentersWiderCanopyOverPet()
    {
        // Act
        var placement = CanopyPlacement.For(Pet128, 128, 1, CanopyRig.Default);

        // Assert
        placement.Width.Should().Be(192);
        placement.X.Should().Be(68);
        placement.Grips.Should().ContainSingle();
        placement.Grips[0].X.Should().BeApproximately(96, 0.001);
        var gripOnScreen = placement.Y + placement.Grips[0].Y;
        gripOnScreen.Should().BeApproximately(200 + (128 * Pet.DefaultGlideGrip.Y), 1);
    }

    /// <summary>
    /// The dome is above the pet window, clear of its ears, and the window reaches down past the lowest grip.
    /// </summary>
    [Fact]
    public void For_TwoGrips_DomeAbovePetAndWindowReachesBelowLowestGrip()
    {
        // Arrange
        var rig = new CanopyRig(UmbrellaKind.Parachute, TwoPaws);

        // Act
        var placement = CanopyPlacement.For(Pet128, 128, 1, rig);

        // Assert
        var domeBottom = placement.Y + (CanopyPlacement.DomeHeight * placement.Scale);
        domeBottom.Should().BeLessThan(200);
        (placement.Y + placement.Height).Should().BeGreaterThan(200 + (128 * 0.5));
    }

    /// <summary>
    /// The parachute is centered over the middle of the two paws, and its strings go to each paw.
    /// </summary>
    [Fact]
    public void For_ParachuteWithTwoGrips_CentersOverTheirMiddle()
    {
        // Arrange
        var rig = new CanopyRig(UmbrellaKind.Parachute, [new FramePoint(0.1, 0.4), new FramePoint(0.5, 0.4)]);

        // Act
        var placement = CanopyPlacement.For(Pet128, 128, 1, rig);

        // Assert
        // The middle of the paws is 0.3 of the way across the pet: 100 + 38.4 px, and the canopy is 192 px wide.
        placement.X.Should().Be(100 + 38 - 96);
        placement.Grips.Select(grip => grip.X + placement.X).Should().Equal([100 + 12.8, 100 + 64], (a, b) => Math.Abs(a - b) < 1);
    }

    /// <summary>
    /// The umbrella is centered over the paw further right, which holds its shaft.
    /// </summary>
    [Fact]
    public void For_UmbrellaWithTwoGrips_CentersOverTheRightPaw()
    {
        // Arrange
        var rig = new CanopyRig(UmbrellaKind.Umbrella, TwoPaws);

        // Act
        var placement = CanopyPlacement.For(Pet128, 128, 1, rig);

        // Assert
        placement.Grips.Max(grip => grip.X).Should().BeApproximately(placement.Width / 2, 0.001);
        (placement.X + (placement.Width / 2)).Should().BeApproximately(100 + (128 * 0.8), 1);
    }

    /// <summary>
    /// A pet shown facing the other way has its frames mirrored, and so are the points it holds on at.
    /// </summary>
    [Fact]
    public void RigFor_Mirrored_FlipsTheGripsLeftToRight()
    {
        // Act
        var rig = CanopyRig.For(UmbrellaKind.Umbrella, TwoPaws, mirrored: true);

        // Assert
        rig.Grips.Select(grip => grip.X).Should().Equal([0.8, 0.2], (a, b) => Math.Abs(a - b) < 1e-9);
        rig.Grips.Select(grip => grip.Y).Should().Equal(0.45, 0.5);
    }

    /// <summary>
    /// A small pet still gets a canopy of a visible size.
    /// </summary>
    [Fact]
    public void For_SmallPet_UsesMinimumCanopyWidth()
    {
        // Act
        var placement = CanopyPlacement.For(new PixelRect(0, 0, 40, 40), 40, 1, CanopyRig.Default);

        // Assert
        placement.Width.Should().Be(90);
    }

    /// <summary>
    /// On a scaled display the canopy window's position is worked out in device pixels, from the pet's pixel bounds,
    /// and its size in device-independent pixels.
    /// </summary>
    [Fact]
    public void For_ScaledDisplay_PlacesInDevicePixels()
    {
        // Arrange
        // 150% scaling: a 100 DIP pet is 150 px wide; the canopy is 150 DIP wide, 225 px.
        var petBounds = new PixelRect(300, 400, 450, 550);

        // Act
        var placement = CanopyPlacement.For(petBounds, 100, 1.5, CanopyRig.Default);

        // Assert
        placement.Width.Should().Be(150);
        placement.X.Should().Be(300 + 75 - 113);
        var gripOnScreen = placement.Y + (placement.Grips[0].Y * 1.5);
        gripOnScreen.Should().BeApproximately(400 + (150 * Pet.DefaultGlideGrip.Y), 1);
    }

    /// <summary>
    /// Moving the pet moves the canopy by the same distance, so a pet and a canopy moved together stay together.
    /// </summary>
    [Fact]
    public void For_PetMoved_CanopyMovesByTheSameDistance()
    {
        // Arrange
        var after = new PixelRect(110, 260, 238, 388);
        var rig = new CanopyRig(UmbrellaKind.Parachute, TwoPaws);

        // Act
        var first = CanopyPlacement.For(Pet128, 128, 1, rig);
        var second = CanopyPlacement.For(after, 128, 1, rig);

        // Assert
        (second.X - first.X).Should().Be(10);
        (second.Y - first.Y).Should().Be(60);
        second.Grips.Should().Equal(first.Grips);
    }
}
