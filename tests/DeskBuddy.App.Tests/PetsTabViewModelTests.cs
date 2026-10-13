using AwesomeAssertions;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.ViewModels;
using DeskBuddy.Application;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PetsTabViewModel"/>.
/// </summary>
public class PetsTabViewModelTests
{
    /// <summary>
    /// The storage behind the settings.
    /// </summary>
    private readonly FakeSettingsStore _store = new();

    /// <summary>
    /// The pet loader behind the tab and the pet switcher.
    /// </summary>
    private readonly FakePetLoader _loader = new();

    /// <summary>
    /// The pets the catalog lists.
    /// </summary>
    private readonly ListedPets _catalog = new();

    /// <summary>
    /// The settings service.
    /// </summary>
    private SettingsService _settings = null!;

    /// <summary>
    /// The view model under test, created by <see cref="CreateAsync"/>.
    /// </summary>
    private PetsTabViewModel _viewModel = null!;

    /// <summary>
    /// A refresh lists the installed pets in the order of the catalog, and marks the one in use.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_InstalledPets_ListsThemAndMarksTheCurrentOne()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal(PetSettings.DefaultPetName, "cat");
        _viewModel.Cards.Select(card => card.IsCurrent).Should().Equal(true, false);
        _viewModel.IsEmpty.Should().BeFalse();
    }

    /// <summary>
    /// With no pet installed the tab says so.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_NoPets_IsEmpty()
    {
        // Arrange
        await CreateAsync();

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Should().BeEmpty();
        _viewModel.IsEmpty.Should().BeTrue();
    }

    /// <summary>
    /// A pet that cannot be loaded is listed with the reason, and cannot be chosen.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetCannotBeLoaded_ListsItWithTheReason()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "broken");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        var broken = _viewModel.Cards.Single(card => card.FolderName == "broken");
        broken.IsAvailable.Should().BeFalse();
        broken.LoadError.Should().Contain("broken");
    }

    /// <summary>
    /// The card shows the name from the pet's manifest but selects by the name of its folder.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ManifestNameDiffersFromFolder_ShowsManifestNameAndKeepsFolder()
    {
        // Arrange
        await CreateAsync("cat-folder");
        _loader.DisplayNames["cat-folder"] = "Tom";

        // Act
        await _viewModel.RefreshAsync();
        await _viewModel.UseCommand.ExecuteAsync(_viewModel.Cards.Single());

        // Assert
        _viewModel.Cards.Single().DisplayName.Should().Be("Tom");
        _settings.Current.PetName.Should().Be("cat-folder");
    }

    /// <summary>
    /// Refreshing again does not load the pets that are already loaded.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_Twice_LoadsEachPetOnce()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _loader.Requested.Should().Equal(PetSettings.DefaultPetName, "cat");
    }

    /// <summary>
    /// A pet that appears after the first refresh is picked up by the next, and one that was removed goes.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetsInstalledAndRemoved_FollowsTheCatalog()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();
        _loader.Available.Add("fox");
        _catalog.Names = [PetSettings.DefaultPetName, "fox"];

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal(PetSettings.DefaultPetName, "fox");
    }

    /// <summary>
    /// A pet that could not be loaded gets another try once it has been repaired.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_BrokenPetRepaired_IsLoadedAgain()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "broken");
        await _viewModel.RefreshAsync();
        _loader.Available.Add("broken");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Single(card => card.FolderName == "broken").IsAvailable.Should().BeTrue();
    }

    /// <summary>
    /// Picking a pet applies it and moves the mark to it.
    /// </summary>
    [Fact]
    public async Task UseCommand_AvailablePet_IsAppliedAndMarked()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();
        var cat = _viewModel.Cards.Single(card => card.FolderName == "cat");

        // Act
        await _viewModel.UseCommand.ExecuteAsync(cat);
        await EventuallyAsync(() => _loader.Requested.Count(name => name == "cat") > 1);

        // Assert
        _settings.Current.PetName.Should().Be("cat");
        _viewModel.Cards.Select(card => card.IsCurrent).Should().Equal(false, true);
        _viewModel.PetError.Should().BeEmpty();
    }

    /// <summary>
    /// Picking the pet already in use changes nothing.
    /// </summary>
    [Fact]
    public async Task UseCommand_CurrentPet_SavesNothing()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();
        var savesBefore = _store.SaveCount;

        // Act
        await _viewModel.UseCommand.ExecuteAsync(_viewModel.Cards[0]);

        // Assert
        _store.SaveCount.Should().Be(savesBefore);
    }

    /// <summary>
    /// A pet that could not be loaded cannot be picked.
    /// </summary>
    [Fact]
    public async Task UseCommand_BrokenPet_ChangesNothing()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "broken");
        await _viewModel.RefreshAsync();

        // Act
        await _viewModel.UseCommand.ExecuteAsync(_viewModel.Cards.Single(card => card.FolderName == "broken"));

        // Assert
        _settings.Current.PetName.Should().Be(PetSettings.DefaultPetName);
    }

    /// <summary>
    /// A pet that loaded for the preview but cannot be switched to goes back to the one in use, with a message.
    /// </summary>
    [Fact]
    public async Task UseCommand_SwitchFails_RevertsMarkAndExplains()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();
        _loader.Available.Remove("cat");
        var cat = _viewModel.Cards.Single(card => card.FolderName == "cat");

        // Act
        await _viewModel.UseCommand.ExecuteAsync(cat);
        await EventuallyAsync(() => _viewModel.PetError.Length > 0 && _viewModel.Cards[0].IsCurrent);

        // Assert
        _settings.Current.PetName.Should().Be(PetSettings.DefaultPetName);
        _viewModel.Cards.Select(card => card.IsCurrent).Should().Equal(true, false);
        _viewModel.PetError.Should().NotBeEmpty();
    }

    /// <summary>
    /// After a refresh the pet in use is the one being previewed.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_Always_PreviewsThePetInUse()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { PetName = "cat" };
        await CreateAsync(PetSettings.DefaultPetName, "cat");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.SelectedCard!.FolderName.Should().Be("cat");
        _viewModel.Cards.Select(card => card.IsSelected).Should().Equal(false, true);
    }

    /// <summary>
    /// If the pet in use cannot be loaded, the first pet that can is previewed instead.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetInUseIsBroken_PreviewsFirstAvailablePet()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { PetName = "broken" };
        await CreateAsync("broken", "cat");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.SelectedCard!.FolderName.Should().Be("cat");
    }

    /// <summary>
    /// Clicking a pet previews it without switching to it.
    /// </summary>
    [Fact]
    public async Task SelectCard_OtherPet_PreviewsItWithoutSwitching()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();
        var savesBefore = _store.SaveCount;
        var cat = _viewModel.Cards.Single(card => card.FolderName == "cat");

        // Act
        _viewModel.SelectCard(cat);

        // Assert
        _viewModel.SelectedCard.Should().BeSameAs(cat);
        _viewModel.Cards.Select(card => card.IsSelected).Should().Equal(false, true);
        _viewModel.Cards.Select(card => card.IsCurrent).Should().Equal(true, false);
        _settings.Current.PetName.Should().Be(PetSettings.DefaultPetName);
        _store.SaveCount.Should().Be(savesBefore);
        _loader.Requested.Should().Equal(PetSettings.DefaultPetName, "cat");
    }

    /// <summary>
    /// A pet that could not be loaded cannot be previewed.
    /// </summary>
    [Fact]
    public async Task SelectCard_BrokenPet_KeepsThePreviousSelection()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "broken");
        await _viewModel.RefreshAsync();

        // Act
        _viewModel.SelectCard(_viewModel.Cards.Single(card => card.FolderName == "broken"));

        // Assert
        _viewModel.SelectedCard!.FolderName.Should().Be(PetSettings.DefaultPetName);
    }

    /// <summary>
    /// Only the picked pet's preview moves; the others stand still.
    /// </summary>
    [Fact]
    public async Task Tick_StateDurationElapsed_MovesOnlyThePickedPet()
    {
        // Arrange
        _loader.ExtraStates[PetSettings.DefaultPetName] = [PetState.Walk];
        _loader.ExtraStates["cat"] = [PetState.Walk];
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();

        // Act
        _viewModel.Tick(PetCardViewModel.StateDuration);

        // Assert
        _viewModel.Cards.Select(card => card.CurrentState).Should().Equal(PetState.Walk, PetState.Idle);
    }

    /// <summary>
    /// Picking another pet stops the one that was moving and puts it back on its first picture.
    /// </summary>
    [Fact]
    public async Task SelectCard_AfterTick_PutsPreviousPetBackOnFirstState()
    {
        // Arrange
        _loader.ExtraStates[PetSettings.DefaultPetName] = [PetState.Walk];
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();
        _viewModel.Tick(PetCardViewModel.StateDuration);

        // Act
        _viewModel.SelectCard(_viewModel.Cards.Single(card => card.FolderName == "cat"));
        _viewModel.Tick(PetCardViewModel.StateDuration);

        // Assert
        _viewModel.Cards[0].CurrentState.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// The pet being previewed stays picked when the list is refreshed.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_AfterPickingAPet_KeepsThePick()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();
        _viewModel.SelectCard(_viewModel.Cards.Single(card => card.FolderName == "cat"));

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.SelectedCard!.FolderName.Should().Be("cat");
    }

    /// <summary>
    /// Creates the view model over fakes.
    /// </summary>
    /// <param name="pets">
    /// The pet names the catalog lists; all of them load except one named <c>broken</c>.
    /// </param>
    /// <returns>
    /// A task that completes when the view model is ready.
    /// </returns>
    private async Task CreateAsync(params string[] pets)
    {
        _settings = new SettingsService(_store);
        await _settings.InitializeAsync();

        _catalog.Names = pets;
        foreach (var pet in pets.Where(pet => pet != "broken"))
        {
            _loader.Available.Add(pet);
        }

        var switcher = new PetSwitcher(
            _settings,
            _loader,
            "pets",
            _settings.Current.PetName,
            _ => { },
            NullLogger<PetSwitcher>.Instance);
        _viewModel = new PetsTabViewModel(_settings, _catalog, _loader, "pets", switcher, _ => null);
    }

    /// <summary>
    /// Waits, briefly, for something that happens after a hand-off to another continuation.
    /// </summary>
    /// <param name="condition">
    /// The condition to wait for.
    /// </param>
    /// <returns>
    /// A task that completes when the condition holds, or after a timeout.
    /// </returns>
    private static async Task EventuallyAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// A pet catalog whose list can change between refreshes.
    /// </summary>
    private sealed class ListedPets : IPetCatalog
    {
        /// <summary>
        /// Gets or sets the names of the installed pets.
        /// </summary>
        public IReadOnlyList<string> Names { get; set; } = [];

        /// <inheritdoc />
        public IReadOnlyList<string> GetPetNames()
        {
            return Names;
        }
    }
}
