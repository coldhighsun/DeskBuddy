namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="CanopyPlacement"/>.
/// </summary>
public sealed class CanopyPlacementTests
{
    /// <summary>
    /// The canopy is one and a half times as wide as the pet, centered over it, with the bottom of the handle a little way
    /// down the pet window so it meets the head.
    /// </summary>
    [Fact]
    public void For_NormalPet_CentersWiderCanopyAboveHead()
    {
        // Arrange
        var petBounds = new PixelRect(100, 200, 228, 328);

        // Act
        var placement = CanopyPlacement.For(petBounds, 128, 1);

        // Assert
        placement.Width.Should().Be(192);
        placement.Height.Should().BeApproximately(153.6, 0.001);
        placement.X.Should().Be(68);
        placement.Y.Should().Be(61);
    }

    /// <summary>
    /// A small pet still gets a canopy of a visible size.
    /// </summary>
    [Fact]
    public void For_SmallPet_UsesMinimumCanopyWidth()
    {
        // Arrange
        var petBounds = new PixelRect(0, 0, 40, 40);

        // Act
        var placement = CanopyPlacement.For(petBounds, 40, 1);

        // Assert
        placement.Width.Should().Be(90);
        placement.Height.Should().Be(72);
    }

    /// <summary>
    /// On a scaled display the canopy window's position is worked out in device pixels, from the pet's pixel bounds.
    /// </summary>
    [Fact]
    public void For_ScaledDisplay_PlacesInDevicePixels()
    {
        // Arrange
        // 150% scaling: a 100 DIP pet is 150 px wide; the canopy is 150 DIP wide and 120 DIP tall, 225 x 180 px.
        var petBounds = new PixelRect(300, 400, 450, 550);

        // Act
        var placement = CanopyPlacement.For(petBounds, 100, 1.5);

        // Assert
        placement.Width.Should().Be(150);
        placement.Height.Should().Be(120);
        placement.X.Should().Be(263);
        placement.Y.Should().Be(238);
    }

    /// <summary>
    /// Moving the pet moves the canopy by the same distance, so a pet and a canopy moved together stay together.
    /// </summary>
    [Fact]
    public void For_PetMoved_CanopyMovesByTheSameDistance()
    {
        // Arrange
        var before = new PixelRect(100, 200, 228, 328);
        var after = new PixelRect(110, 260, 238, 388);

        // Act
        var first = CanopyPlacement.For(before, 128, 1);
        var second = CanopyPlacement.For(after, 128, 1);

        // Assert
        (second.X - first.X).Should().Be(10);
        (second.Y - first.Y).Should().Be(60);
    }
}
