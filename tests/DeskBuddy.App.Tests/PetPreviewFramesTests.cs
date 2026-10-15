using System.IO;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.ViewModels;
using DeskBuddy.Infrastructure.Pets;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PetPreviewFrames"/>, and for the pets tab over the pets shipped in the repository.
/// </summary>
public class PetPreviewFramesTests
{
    /// <summary>
    /// A picture file is decoded to a frozen image, and asking again gives the same one.
    /// </summary>
    [Fact]
    public void Get_ShippedPicture_DecodesOnceToFrozenImage()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var frames = new PetPreviewFrames();
            var path = Path.Combine(RepositoryPaths.Pets, "robot", "idle_0.png");

            // Act
            var first = frames.Get(path);
            var second = frames.Get(path);

            // Assert
            first.Should().NotBeNull();
            first!.IsFrozen.Should().BeTrue();
            second.Should().BeSameAs(first);
        });
    }

    /// <summary>
    /// A file that is not there leaves a gap in the preview instead of failing.
    /// </summary>
    [Fact]
    public void Get_MissingFile_ReturnsNull()
    {
        StaThread.Run(() =>
        {
            // Act
            var image = new PetPreviewFrames().Get(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nothing.png"));

            // Assert
            image.Should().BeNull();
        });
    }

    /// <summary>
    /// The pets tab lists the shipped pets and every preview has a real picture, whichever state it is showing.
    /// </summary>
    [Fact]
    public void PetsTab_ShippedPets_EveryPreviewShowsPictureInEveryState()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var petsDirectory = RepositoryPaths.Pets;
            var settings = new SettingsService(new FakeSettingsStore());
            settings.InitializeAsync().GetAwaiter().GetResult();
            var loader = new FolderPetLoader(NullLogger<FolderPetLoader>.Instance);
            var switcher = new PetSwitcher(
                settings,
                loader,
                petsDirectory,
                settings.Current.PetName,
                _ => { },
                NullLogger<PetSwitcher>.Instance);
            var pets = new PetsTabViewModel(
                settings,
                new FolderPetCatalog(petsDirectory),
                loader,
                petsDirectory,
                switcher,
                new PetPreviewFrames().Get);

            // Act
            pets.RefreshAsync().GetAwaiter().GetResult();

            // Assert
            pets.Cards.Select(card => card.FolderName).Should().Contain([PetSettings.DefaultPetName, "cat"]);
            pets.Cards.Should().OnlyContain(card => card.IsAvailable);
            pets.Cards.Single(card => card.IsCurrent).FolderName.Should().Be(PetSettings.DefaultPetName);
            for (var step = 0; step < 12; step++)
            {
                pets.Cards.Should().OnlyContain(card => card.CurrentFrame != null);
                pets.Tick(PetCardViewModel.StateDuration);
            }
        });
    }
}
