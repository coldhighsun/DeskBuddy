using System.Windows.Media;
using AwesomeAssertions;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Resources;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PetSizeChoices"/>.
/// </summary>
public class PetSizeChoicesTests
{
    /// <summary>
    /// A picture to show; its content does not matter to these tests.
    /// </summary>
    private static readonly ImageSource Picture = new DrawingImage();

    /// <summary>
    /// The pets the tests choose from.
    /// </summary>
    private static readonly InstalledPet[] Pets =
    [
        new("cat", "Cat", Picture, 64, 64),
        new("robot", "Robot", Picture, 128, 160),
    ];

    /// <summary>
    /// Every size is offered, smallest first, and the size in use is marked.
    /// </summary>
    [Fact]
    public void For_SizeInUse_MarksOnlyThatSize()
    {
        // Act
        var choices = PetSizeChoices.For(Pets, "cat", PetSize.Medium);

        // Assert
        choices.Select(choice => choice.Size).Should().Equal(PetSize.Small, PetSize.Medium, PetSize.Large);
        choices.Select(choice => choice.IsCurrent).Should().Equal(false, true, false);
    }

    /// <summary>
    /// The measure of each size is the size the pet in use has on screen, which follows the pet's own frame size.
    /// </summary>
    [Fact]
    public void For_PetInUse_MeasuresPetAtEachSize()
    {
        // Act
        var choices = PetSizeChoices.For(Pets, "cat", PetSize.Large);

        // Assert
        choices.Select(choice => choice.Width).Should().Equal(32, 48, 64);
        choices.Select(choice => choice.Height).Should().Equal(32, 48, 64);
    }

    /// <summary>
    /// Another pet gives other measures: nothing is taken from a particular pet.
    /// </summary>
    [Fact]
    public void For_SmallerPetInUse_UsesItsFrameSize()
    {
        // Arrange
        InstalledPet[] pets = [new("mouse", "Mouse", Picture, 32, 32)];

        // Act
        var choices = PetSizeChoices.For(pets, "mouse", PetSize.Small);

        // Assert
        choices.Select(choice => choice.Width).Should().Equal(16, 24, 32);
        choices.Select(choice => choice.Height).Should().Equal(16, 24, 32);
    }

    /// <summary>
    /// A pet with a big frame is drawn smaller, all sizes by the same factor, so the list stays small enough for the
    /// screen and the sizes keep their proportions.
    /// </summary>
    [Fact]
    public void For_BigPet_DrawsAllSizesSmallerByTheSameFactor()
    {
        // Arrange
        InstalledPet[] pets = [new("giant", "Giant", Picture, 256, 320)];

        // Act
        var choices = PetSizeChoices.For(pets, "giant", PetSize.Medium);

        // Assert
        choices.Select(choice => choice.Width).Should().Equal(38, 58, 77);
        choices.Select(choice => choice.Height).Should().Equal(48, 72, 96);
    }

    /// <summary>
    /// A pet whose biggest size is exactly the longest edge allowed is not drawn smaller.
    /// </summary>
    [Fact]
    public void For_PetAtTheLimit_IsNotShrunk()
    {
        // Arrange
        InstalledPet[] pets = [new("limit", "Limit", Picture, 96, 64)];

        // Act
        var choices = PetSizeChoices.For(pets, "limit", PetSize.Large);

        // Assert
        choices.Select(choice => choice.Width).Should().Equal(48, 72, 96);
        choices.Select(choice => choice.Height).Should().Equal(32, 48, 64);
    }

    /// <summary>
    /// Without a pet in use there is nothing to measure, but the sizes are still offered and the one in use is marked.
    /// </summary>
    [Fact]
    public void For_PetInUseMissing_HasNoPictureAndNoMeasure()
    {
        // Act
        var choices = PetSizeChoices.For(Pets, "dog", PetSize.Large);

        // Assert
        choices.Should().HaveCount(3);
        choices.Should().OnlyContain(choice => choice.Picture == null && choice.Width == 0 && choice.Height == 0);
        choices.Select(choice => choice.IsCurrent).Should().Equal(false, false, true);
    }

    /// <summary>
    /// The sizes are named in the language of the interface.
    /// </summary>
    [Fact]
    public void For_Sizes_AreLabelledWithTheirNames()
    {
        // Act
        var choices = PetSizeChoices.For(Pets, "cat", PetSize.Small);

        // Assert
        choices.Select(choice => choice.Label).Should().Equal(Strings.PetSizeSmall, Strings.PetSizeMedium, Strings.PetSizeLarge);
    }

    /// <summary>
    /// A pet without a picture is not given a measure, as an empty box of its size would only be in the way.
    /// </summary>
    [Fact]
    public void For_PetWithoutPicture_HasNoMeasure()
    {
        // Arrange
        InstalledPet[] pets = [new("blank", "Blank", null, 64, 64)];

        // Act
        var choices = PetSizeChoices.For(pets, "blank", PetSize.Medium);

        // Assert
        choices.Should().OnlyContain(choice => choice.Picture == null && choice.Width == 0 && choice.Height == 0);
        choices.Select(choice => choice.IsCurrent).Should().Equal(false, true, false);
    }
}
