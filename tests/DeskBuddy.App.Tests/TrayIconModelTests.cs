using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="TrayIconModel"/>.
/// </summary>
public class TrayIconModelTests
{
    /// <summary>
    /// The idle picture of a pet.
    /// </summary>
    private static readonly ImageSource Idle = Picture();

    /// <summary>
    /// The picture a pet brings for the icon.
    /// </summary>
    private static readonly ImageSource Head = Picture();

    /// <summary>
    /// The idle picture of the fallback pet.
    /// </summary>
    private static readonly ImageSource FallbackIdle = Picture();

    /// <summary>
    /// A pet that brings an icon picture has it used as it is, in preference to its idle picture.
    /// </summary>
    [Fact]
    public void Choose_PetWithIconPicture_UsesItAsTheIcon()
    {
        // Arrange
        InstalledPet[] entries = [new("cat", "cat", Idle, 64, 64, Head)];

        // Act
        var choice = TrayIconModel.Choose(entries, "cat", "robot");

        // Assert
        choice.Picture.Should().BeSameAs(Head);
        choice.IsIconPicture.Should().BeTrue();
    }

    /// <summary>
    /// A pet without an icon picture falls back to its idle picture, of which only a part is used.
    /// </summary>
    [Fact]
    public void Choose_PetWithoutIconPicture_UsesIdlePicture()
    {
        // Arrange
        InstalledPet[] entries = [new("cat", "cat", Idle, 64, 64)];

        // Act
        var choice = TrayIconModel.Choose(entries, "cat", "robot");

        // Assert
        choice.Picture.Should().BeSameAs(Idle);
        choice.IsIconPicture.Should().BeFalse();
    }

    /// <summary>
    /// A pet that is not among the pets that could be loaded gives the icon of the fallback pet, which is the pet that is
    /// shown instead.
    /// </summary>
    [Fact]
    public void Choose_PetNotListed_UsesTheFallbackPet()
    {
        // Arrange
        InstalledPet[] entries = [new("cat", "cat", Idle, 64, 64, Head), new("robot", "robot", FallbackIdle, 64, 64)];

        // Act
        var choice = TrayIconModel.Choose(entries, "missing", "robot");

        // Assert
        choice.Picture.Should().BeSameAs(FallbackIdle);
        choice.IsIconPicture.Should().BeFalse();
    }

    /// <summary>
    /// With no pet to take a picture from the icon is the application's own, which is a picture of null.
    /// </summary>
    [Fact]
    public void Choose_NoPets_HasNoPicture()
    {
        // Act
        var choice = TrayIconModel.Choose([], "cat", "robot");

        // Assert
        choice.Picture.Should().BeNull();
        choice.IsIconPicture.Should().BeFalse();
    }

    /// <summary>
    /// A pet without any picture has none to give, even though it is the pet in use.
    /// </summary>
    [Fact]
    public void Choose_PetWithoutPictures_HasNoPicture()
    {
        // Arrange
        InstalledPet[] entries = [new("cat", "cat", null, 64, 64)];

        // Act
        var choice = TrayIconModel.Choose(entries, "cat", "robot");

        // Assert
        choice.Picture.Should().BeNull();
    }

    /// <summary>
    /// Makes a picture to tell the pictures apart by reference.
    /// </summary>
    /// <returns>
    /// The picture.
    /// </returns>
    private static BitmapSource Picture()
    {
        var picture = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        picture.Freeze();
        return picture;
    }
}
