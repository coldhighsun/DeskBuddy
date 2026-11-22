namespace DeskBuddy.App.Tests.Presentation;

/// <summary>
/// Tests for <see cref="EffectLayout"/>. The pets here are made up of numbers: the layout must fit a pet of any shape.
/// </summary>
public sealed class EffectLayoutTests
{
    /// <summary>
    /// The work area used by the tests: a 1920 x 1040 screen with a taskbar below.
    /// </summary>
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);

    /// <summary>
    /// The window is centered on the pet sideways, reaches from the top of the work area to a little below the pet's feet,
    /// and holds the pet where the pet is.
    /// </summary>
    [Fact]
    public void For_PetOnTheGround_CentersTheWindowAndReachesTheTop()
    {
        // Arrange
        var pet = new PixelRect(900, 940, 1000, 1040);

        // Act
        var layout = EffectLayout.For(pet, WorkArea, 1);

        // Assert
        (layout.X + (layout.WidthPixels / 2)).Should().Be(950);
        layout.Y.Should().Be(0);
        (layout.Y + layout.HeightPixels).Should().Be(pet.Bottom + 75);
        layout.GroundPad.Should().Be(75);
        layout.PetLeft.Should().BeApproximately(layout.Width / 2 - 50, 0.5);
        layout.PetTop.Should().Be(940);
        layout.PetWidth.Should().Be(100);
        layout.PetHeight.Should().Be(100);
    }

    /// <summary>
    /// A pet of another shape and size gets a window and a place in it that follow its own measures.
    /// </summary>
    /// <param name="width">The pet's width in pixels.</param>
    /// <param name="height">The pet's height in pixels.</param>
    [Theory]
    [InlineData(40, 40)]
    [InlineData(300, 120)]
    [InlineData(80, 400)]
    public void For_PetsOfAnyShape_WindowHoldsThePetAndIsWideEnough(int width, int height)
    {
        // Arrange
        var pet = new PixelRect(700, 1040 - height, 700 + width, 1040);

        // Act
        var layout = EffectLayout.For(pet, WorkArea, 1);

        // Assert
        layout.PetLeft.Should().BeGreaterThanOrEqualTo(0);
        (layout.PetLeft + layout.PetWidth).Should().BeLessThanOrEqualTo(layout.Width);
        layout.Width.Should().BeGreaterThanOrEqualTo(2.4 * Math.Max(width, height) - 1);
        layout.Unit.Should().Be(Math.Max(width, height));
    }

    /// <summary>
    /// On a monitor with a larger scale the pixels are turned into device-independent units, so the drawings keep their
    /// size relative to the pet.
    /// </summary>
    [Fact]
    public void For_ScaledMonitor_MeasuresInDeviceIndependentPixels()
    {
        // Arrange
        var pet = new PixelRect(900, 840, 1100, 1040);

        // Act
        var layout = EffectLayout.For(pet, WorkArea, 2);

        // Assert
        layout.PetWidth.Should().Be(100);
        layout.PetHeight.Should().Be(100);
        layout.GroundPad.Should().BeApproximately(75, 0.5);
        layout.Scale.Should().Be(2);
        layout.Width.Should().BeApproximately(layout.WidthPixels / 2.0, 0.001);
    }

    /// <summary>
    /// A pet above the top of the work area (on a monitor above, or hanging from the ceiling) still gets a window that
    /// holds it.
    /// </summary>
    [Fact]
    public void For_PetAboveTheWorkArea_WindowStartsAtThePet()
    {
        // Arrange
        var pet = new PixelRect(500, -60, 600, 40);

        // Act
        var layout = EffectLayout.For(pet, WorkArea, 1);

        // Assert
        layout.Y.Should().Be(-60);
        layout.PetTop.Should().Be(0);
        layout.HeightPixels.Should().Be(100 + 75);
    }
}
