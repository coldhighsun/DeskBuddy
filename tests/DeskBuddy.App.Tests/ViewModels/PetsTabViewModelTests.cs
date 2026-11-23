using DeskBuddy.App.Resources;
using System.IO;
using Microsoft.Extensions.Logging;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.ViewModels;
using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.App.Tests.ViewModels;

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
    private readonly FakePetCatalog _catalog = new();

    /// <summary>
    /// The names of the pictures the tab asked to have cut out ahead of time.
    /// </summary>
    private readonly List<string> _preloaded = [];

    /// <summary>
    /// The sets of picture names the tab asked to keep, one for each time it did.
    /// </summary>
    private readonly List<string[]> _retained = [];

    /// <summary>
    /// The questions the tab asked before deleting a pet: the pet, and whether the question was about deleting for good.
    /// </summary>
    private readonly List<(string Name, bool Permanently)> _confirmations = [];

    /// <summary>
    /// The answers to give to the questions one after the other; once they are used up, <see cref="_confirmAnswer"/>.
    /// </summary>
    private readonly Queue<bool> _confirmAnswers = new();

    /// <summary>
    /// The answer the user gives to the question about deleting a pet, unless <see cref="_confirmAnswers"/> says otherwise.
    /// </summary>
    private bool _confirmAnswer = true;

    /// <summary>
    /// What the preload fails with, or <see langword="null"/> if it works.
    /// </summary>
    private Exception? _preloadFailure;

    /// <summary>
    /// A picture to give for any file, as the pictures that were read.
    /// </summary>
    private static readonly System.Windows.Media.Imaging.BitmapSource Picture = MakePicture();

    /// <summary>
    /// Whether the pictures can be read: when not, the pets load but have no picture, as with a corrupt atlas.
    /// </summary>
    private bool _picturesAvailable = true;

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
    /// After the interface language changed, a refresh makes the cards again, as they hold text in the language they were
    /// made in; the pet being previewed stays picked.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_LanguageChanged_MakesTheCardsAgain()
    {
        // Arrange
        var original = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en");
            await CreateAsync(PetSettings.DefaultPetName, "cat");
            await _viewModel.RefreshAsync();
            var before = _viewModel.Cards.ToList();
            _viewModel.SelectCard(before[1]);

            // Act
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            await _viewModel.RefreshAsync();

            // Assert
            _viewModel.Cards.Should().NotIntersectWith(before);
            _viewModel.SelectedCard!.FolderName.Should().Be("cat");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = original;
        }
    }

    /// <summary>
    /// While the interface language stays the same, a refresh keeps the cards that were made.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_LanguageUnchanged_KeepsTheCards()
    {
        // Arrange
        await CreateAsync(PetSettings.DefaultPetName, "cat");
        await _viewModel.RefreshAsync();
        var before = _viewModel.Cards.ToList();

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Should().Equal(before);
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
    /// Before the first look at the pets the tab is loading, and does not claim that no pet is installed; afterwards it is
    /// not loading any more.
    /// </summary>
    [Fact]
    public async Task IsLoading_BeforeAndAfterFirstRefresh_IsTrueThenFalseWithoutBeingEmpty()
    {
        // Arrange
        await CreateAsync();

        // Assert
        _viewModel.IsLoading.Should().BeTrue();
        _viewModel.IsEmpty.Should().BeFalse();

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.IsLoading.Should().BeFalse();
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
        broken.LoadError.Should().Contain(Strings.ErrorInvalidData);
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
    /// With a folder for the user's own pets, the tab offers to open it and does so when asked.
    /// </summary>
    [Fact]
    public async Task OpenPetsFolderCommand_FolderGiven_OpensIt()
    {
        // Arrange
        await CreateAsync();
        var opened = 0;
        var tab = new PetsTabViewModel(
            _settings,
            _catalog,
            _loader,
            new PetSwitcher(_settings, _loader, _catalog, _settings.Current.PetName, _ => { }, NullLogger<PetSwitcher>.Instance),
            _ => null,
            openPetsFolder: () => opened++);

        // Act
        tab.OpenPetsFolderCommand.Execute(null);

        // Assert
        tab.CanOpenPetsFolder.Should().BeTrue();
        opened.Should().Be(1);
    }

    /// <summary>
    /// Without a folder for the user's own pets there is nothing to open.
    /// </summary>
    [Fact]
    public async Task CanOpenPetsFolder_NoFolder_IsFalse()
    {
        // Arrange
        await CreateAsync();

        // Act
        var canOpen = _viewModel.CanOpenPetsFolder;

        // Assert
        canOpen.Should().BeFalse();
    }

    /// <summary>
    /// A failure to list the pets in a quiet refresh is only logged; the task completes and the tab stops loading, so nothing is
    /// left unobserved to show up as a crash.
    /// </summary>
    [Fact]
    public async Task RefreshQuietlyAsync_CatalogFails_LogsAndCompletes()
    {
        // Arrange
        await CreateAsync();
        var failing = new PetsTabViewModel(
            _settings,
            new FailingCatalog(),
            _loader,
            new PetSwitcher(_settings, _loader, _catalog, _settings.Current.PetName, _ => { }, NullLogger<PetSwitcher>.Instance),
            _ => null);
        var logger = new RecordingLogger<PetsTabViewModel>();

        // Act
        await failing.RefreshQuietlyAsync(logger);

        // Assert
        logger.Levels.Should().Equal(LogLevel.Warning);
        failing.IsLoading.Should().BeFalse();
    }

    /// <summary>
    /// A pet that loaded but whose pictures could not be read is loaded again by the next refresh, and has its picture once it
    /// can be read (the atlas has been repaired).
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetWithoutPicture_IsLoadedAgainAndPickedUpOnceRepaired()
    {
        // Arrange
        _picturesAvailable = false;
        await CreateAsync("cat");
        await _viewModel.RefreshAsync();
        _viewModel.Cards.Single().HasPicture.Should().BeFalse();
        _loader.Requested.Clear();
        _picturesAvailable = true;

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _loader.Requested.Should().Equal("cat");
        _viewModel.Cards.Single().HasPicture.Should().BeTrue();
        _viewModel.Cards.Single().CurrentFrame.Should().BeSameAs(Picture);
    }

    /// <summary>
    /// Makes a small frozen picture.
    /// </summary>
    /// <returns>The picture.</returns>
    private static System.Windows.Media.Imaging.BitmapSource MakePicture()
    {
        var picture = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
        picture.Freeze();
        return picture;
    }

    /// <summary>
    /// A quiet refresh that works lists the pets like a refresh does.
    /// </summary>
    [Fact]
    public async Task RefreshQuietlyAsync_PetsInstalled_ListsThem()
    {
        // Arrange
        await CreateAsync("cat");

        // Act
        await _viewModel.RefreshQuietlyAsync(NullLogger.Instance);

        // Assert
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal("cat");
    }

    /// <summary>
    /// The pictures of the previews are asked to be cut out ahead of time, before the card is made, so the UI thread does
    /// not decode the atlas.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_Pet_PreloadsItsPreviewPictures()
    {
        // Arrange
        await CreateAsync("cat");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _preloaded.Should().Contain("idle.png");
    }

    /// <summary>
    /// A refresh that finds the same pets does not hand the list over again, as that makes the tab build every card anew,
    /// which shows as a flash each time the panel is opened.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_SamePets_DoesNotReplaceTheList()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        await _viewModel.RefreshAsync();
        var list = _viewModel.Cards;
        var changes = new List<string?>();
        _viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Should().BeSameAs(list);
        changes.Should().NotContain(nameof(PetsTabViewModel.Cards));
    }

    /// <summary>
    /// A pet that still fails in the same way when it is loaded again keeps its card, so a broken pet does not make the
    /// panel build every card anew each time it is opened.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_BrokenPetStillBroken_KeepsTheListAndItsCard()
    {
        // Arrange
        await CreateAsync("cat", "broken");
        await _viewModel.RefreshAsync();
        var list = _viewModel.Cards;
        var brokenCard = _viewModel.Cards.Single(card => card.FolderName == "broken");
        var changes = new List<string?>();
        _viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Should().BeSameAs(list);
        _viewModel.Cards.Single(card => card.FolderName == "broken").Should().BeSameAs(brokenCard);
        changes.Should().NotContain(nameof(PetsTabViewModel.Cards));
    }

    /// <summary>
    /// A broken pet that has been repaired gets a new card, and the list is handed over so that it shows.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_BrokenPetRepaired_GetsANewWorkingCard()
    {
        // Arrange
        await CreateAsync("cat", "broken");
        await _viewModel.RefreshAsync();
        var brokenCard = _viewModel.Cards.Single(card => card.FolderName == "broken");
        brokenCard.IsAvailable.Should().BeFalse();
        _loader.Available.Add("broken");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        var repaired = _viewModel.Cards.Single(card => card.FolderName == "broken");
        repaired.Should().NotBeSameAs(brokenCard);
        repaired.IsAvailable.Should().BeTrue();
    }

    /// <summary>
    /// A pet that fails for another reason than before gets a card that says so.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_BrokenPetFailsDifferently_GetsANewCard()
    {
        // Arrange
        await CreateAsync("cat", "broken");
        await _viewModel.RefreshAsync();
        var brokenCard = _viewModel.Cards.Single(card => card.FolderName == "broken");
        _picturesAvailable = false;
        _loader.Available.Add("broken");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Single(card => card.FolderName == "broken").Should().NotBeSameAs(brokenCard);
    }

    /// <summary>
    /// On the first look at the pets, with the previews already running, what the previews keep is worked out at once:
    /// the cache is not left holding pictures of pets that are not picked.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PreviewsRunningOnTheFirstLook_RetainsThePickedPetsPictures()
    {
        // Arrange
        _loader.ExtraStates["cat"] = [PetState.Walk];
        await CreateAsync("cat");
        _viewModel.RestartPreviews();

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _retained.Should().NotBeEmpty();
        _retained.Last().Distinct().Should().BeEquivalentTo("idle.png", "Walk_0.png", "Walk_1.png");
    }

    /// <summary>
    /// A refresh that finds a pet more does hand the new list over.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetAdded_ReplacesTheList()
    {
        // Arrange
        await CreateAsync("cat");
        await _viewModel.RefreshAsync();
        _loader.Available.Add("dog");
        _catalog.Names = ["cat", "dog"];

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal("cat", "dog");
    }

    /// <summary>
    /// When the previews run while the pets are being looked at (the panel is opened for the first time), the picked pet's
    /// pictures are got ready once the list is made, though the pet that is picked has not changed.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PreviewsRunning_PreparesThePickedPetsPictures()
    {
        // Arrange
        _loader.ExtraStates["cat"] = [PetState.Walk];
        await CreateAsync("cat");
        _viewModel.RestartPreviews();

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        await EventuallyAsync(() => _preloaded.Contains("Walk_0.png"));
        _preloaded.Should().Contain("Walk_0.png");
    }

    /// <summary>
    /// Listing the pets cuts out only the pictures each card rests on, not those of every state of every pet: the panel may
    /// list many pets, and only the picked one moves.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetWithMoreStates_PreloadsOnlyTheRestingPictures()
    {
        // Arrange
        _loader.ExtraStates["cat"] = [PetState.Walk];
        await CreateAsync("cat");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _preloaded.Should().Equal("idle.png");
    }

    /// <summary>
    /// Once the previews run, the picked pet's pictures of every state are cut out ahead of time and kept, besides the
    /// pictures every card rests on.
    /// </summary>
    [Fact]
    public async Task RestartPreviews_PickedPet_PreloadsAndKeepsItsPictures()
    {
        // Arrange
        _loader.ExtraStates["cat"] = [PetState.Walk];
        await CreateAsync("cat", "dog");
        await _viewModel.RefreshAsync();
        _preloaded.Clear();

        // Act
        _viewModel.RestartPreviews();

        // Assert
        await EventuallyAsync(() => _preloaded.Count > 0);
        _preloaded.Should().BeEquivalentTo("idle.png", "Walk_0.png", "Walk_1.png");
        _retained.Last().Distinct().Should().BeEquivalentTo("idle.png", "Walk_0.png", "Walk_1.png");
    }

    /// <summary>
    /// Picking another pet gives back the pictures of the pet that was picked before.
    /// </summary>
    [Fact]
    public async Task SelectCard_PreviewsRunning_KeepsOnlyThePickedPetsPictures()
    {
        // Arrange
        _loader.ExtraStates["cat"] = [PetState.Walk];
        _loader.ExtraStates["dog"] = [PetState.Sleep];
        await CreateAsync("cat", "dog");
        await _viewModel.RefreshAsync();
        _viewModel.RestartPreviews();

        // Act
        _viewModel.SelectCard(_viewModel.Cards.Single(card => card.FolderName == "dog"));

        // Assert
        _retained.Last().Distinct().Should().BeEquivalentTo("idle.png", "Sleep_0.png", "Sleep_1.png");
    }

    /// <summary>
    /// When the previews stop, only the pictures the cards rest on are kept.
    /// </summary>
    [Fact]
    public async Task TrimPreviews_AfterPreviewsRan_KeepsOnlyTheRestingPictures()
    {
        // Arrange
        _loader.ExtraStates["cat"] = [PetState.Walk];
        await CreateAsync("cat");
        await _viewModel.RefreshAsync();
        _viewModel.RestartPreviews();

        // Act
        _viewModel.TrimPreviews();

        // Assert
        _retained.Last().Distinct().Should().BeEquivalentTo("idle.png");
    }

    /// <summary>
    /// While the previews are not running (the panel is hidden), picking a pet does not cut anything out or keep anything.
    /// </summary>
    [Fact]
    public async Task SelectCard_PreviewsNotRunning_DoesNotPreloadOrRetain()
    {
        // Arrange
        _loader.ExtraStates["dog"] = [PetState.Sleep];
        await CreateAsync("cat", "dog");
        await _viewModel.RefreshAsync();
        _preloaded.Clear();
        _retained.Clear();

        // Act
        _viewModel.SelectCard(_viewModel.Cards.Single(card => card.FolderName == "dog"));

        // Assert
        _preloaded.Should().BeEmpty();
        _retained.Should().BeEmpty();
    }

    /// <summary>
    /// A preload that fails is only a lost head start: the preview still works and nothing is thrown.
    /// </summary>
    [Fact]
    public async Task RestartPreviews_PreloadFails_DoesNotThrow()
    {
        // Arrange
        await CreateAsync("cat");
        await _viewModel.RefreshAsync();
        _preloaded.Clear();
        _preloadFailure = new IOException("atlas unreadable");

        // Act
        var act = () => _viewModel.RestartPreviews();

        // Assert
        act.Should().NotThrow();
        await EventuallyAsync(() => _preloaded.Count > 0);
        _preloaded.Should().NotBeEmpty();
    }

    /// <summary>
    /// A pet whose pictures cannot be read ahead of time is shown as one that cannot be used, with the reason.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PreloadFails_ShowsPetAsBroken()
    {
        // Arrange
        await CreateAsync("cat");
        _preloadFailure = new IOException("atlas unreadable");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Single().IsAvailable.Should().BeFalse();
        _viewModel.Cards.Single().LoadError.Should().Contain(Strings.ErrorFileInUse);
    }

    /// <summary>
    /// Pets that the user has switched off in the settings show as switched off, and the pet in use shows as offered
    /// whatever the settings say.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_HiddenPetsInSettings_ShowsThemSwitchedOffExceptThePetInUse()
    {
        // Arrange
        await CreateAsync("cat", "dog", "fox");
        await _settings.UpdateAsync(settings => settings with { PetName = "cat", HiddenPets = ["dog", "cat"] }, TestContext.Current.CancellationToken);

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Single(card => card.FolderName == "dog").IsShown.Should().BeFalse();
        _viewModel.Cards.Single(card => card.FolderName == "fox").IsShown.Should().BeTrue();
        _viewModel.Cards.Single(card => card.FolderName == "cat").IsShown.Should().BeTrue("the pet in use is always offered");
    }

    /// <summary>
    /// Flipping the switch of a pet off puts it on the list of pets that are switched off, and flipping it on takes it off.
    /// </summary>
    [Fact]
    public async Task ToggleShownCommand_SwitchFlipped_UpdatesTheSettings()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");

        // Act
        dog.IsShown = false;
        await _viewModel.ToggleShownCommand.ExecuteAsync(dog);
        var hiddenAfterSwitchingOff = _settings.Current.HiddenPets;
        dog.IsShown = true;
        await _viewModel.ToggleShownCommand.ExecuteAsync(dog);

        // Assert
        hiddenAfterSwitchingOff.Should().Equal("dog");
        _settings.Current.HiddenPets.Should().BeEmpty();
    }

    /// <summary>
    /// Switching a pet off twice (or on twice) does not put it on the list twice.
    /// </summary>
    [Fact]
    public async Task ToggleShownCommand_SwitchedOffTwice_IsOnTheListOnce()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");
        dog.IsShown = false;

        // Act
        await _viewModel.ToggleShownCommand.ExecuteAsync(dog);
        await _viewModel.ToggleShownCommand.ExecuteAsync(dog);

        // Assert
        _settings.Current.HiddenPets.Should().Equal("dog");
    }

    /// <summary>
    /// The switch of the pet in use does nothing: the pet in use is always offered, and the switch is disabled.
    /// </summary>
    [Fact]
    public async Task ToggleShownCommand_PetInUse_ChangesNothing()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        await _settings.UpdateAsync(settings => settings with { PetName = "cat" }, TestContext.Current.CancellationToken);
        await _viewModel.RefreshAsync();
        var cat = _viewModel.Cards.Single(card => card.FolderName == "cat");

        // Act
        cat.IsShown = false;
        await _viewModel.ToggleShownCommand.ExecuteAsync(cat);

        // Assert
        cat.CanChangeShown.Should().BeFalse();
        _settings.Current.HiddenPets.Should().BeEmpty();
    }

    /// <summary>
    /// A pet that could not be loaded is shown as broken, cannot be offered, and has no switch to flip.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_BrokenPet_IsBrokenAndCannotBeOffered()
    {
        // Arrange
        await CreateAsync("cat", "broken");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        var broken = _viewModel.Cards.Single(card => card.FolderName == "broken");
        broken.IsBroken.Should().BeTrue();
        broken.CanChangeShown.Should().BeFalse();
        _viewModel.Cards.Single(card => card.FolderName == "cat").IsBroken.Should().BeFalse();
    }

    /// <summary>
    /// A change of the pet in use (or of the list) in the settings shows on the switches at once.
    /// </summary>
    [Fact]
    public async Task SettingsChanged_PetSwitchedOffElsewhere_ShowsOnTheCard()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");

        // Act
        await _settings.UpdateAsync(settings => settings with { HiddenPets = ["dog"] }, TestContext.Current.CancellationToken);

        // Assert
        dog.IsShown.Should().BeFalse();
    }

    /// <summary>
    /// A pet in the user's folder can be deleted; one that comes with the application, and the pet in use, cannot, and say why.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_Pets_SetWhichCanBeDeletedAndTheReasonForTheOthers()
    {
        // Arrange
        await CreateAsync("cat", "dog", "fox");
        _catalog.Removable.Add("cat");
        _catalog.Removable.Add("dog");
        await _settings.UpdateAsync(settings => settings with { PetName = "cat" }, TestContext.Current.CancellationToken);

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        var cat = _viewModel.Cards.Single(card => card.FolderName == "cat");
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");
        var fox = _viewModel.Cards.Single(card => card.FolderName == "fox");
        dog.CanDeleteNow.Should().BeTrue();
        dog.DeleteToolTip.Should().Be(Strings.PetsDelete);
        cat.CanDeleteNow.Should().BeFalse();
        cat.DeleteToolTip.Should().Be(Strings.PetsDeleteInUse);
        fox.CanDeleteNow.Should().BeFalse();
        fox.DeleteToolTip.Should().Be(Strings.PetsDeleteShipped);
    }

    /// <summary>
    /// After the user agrees, the pet's folder is removed (to the Recycle Bin) and the pet leaves the list.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_UserAgrees_RemovesThePetAndRefreshesTheList()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(dog);

        // Assert
        _confirmations.Should().Equal(("dog", false));
        _catalog.Removals.Should().Equal(("dog", false));
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal("cat");
        _viewModel.PetError.Should().BeEmpty();
    }

    /// <summary>
    /// Deleting the user's copy of a pet that also comes with the application shows the shipped pet again: its card is made
    /// anew instead of the card of the deleted copy being kept under the same name.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_CopyStoodInForAShippedPet_ShowsANewCardForTheShippedOne()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");
        _catalog.NamesStayWhenRemoved = true;
        await _viewModel.RefreshAsync();
        var copy = _viewModel.Cards.Single(card => card.FolderName == "dog");
        _loader.Requested.Clear();

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(copy);

        // Assert
        var shipped = _viewModel.Cards.Single(card => card.FolderName == "dog");
        shipped.Should().NotBeSameAs(copy);
        shipped.CanDelete.Should().BeFalse();
        _loader.Requested.Should().ContainSingle(path => path.EndsWith("dog", StringComparison.Ordinal));
    }

    /// <summary>
    /// When the choice cannot be saved, whatever the reason, the switch goes back to what the settings say.
    /// </summary>
    [Fact]
    public async Task ToggleShownCommand_SettingsRefuseTheChange_SwitchShowsTheSettingsAgain()
    {
        // Arrange
        _store.LoadException = new IOException("unreadable");
        await CreateAsync("cat", "dog");
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");
        dog.IsShown = false;

        // Act
        var act = () => _viewModel.ToggleShownCommand.ExecuteAsync(dog);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        dog.IsShown.Should().BeTrue();
    }

    /// <summary>
    /// When the user says no, nothing is removed.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_UserDeclines_RemovesNothing()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");
        _confirmAnswer = false;
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(dog);

        // Assert
        _confirmations.Should().Equal(("dog", false));
        _catalog.Removals.Should().BeEmpty();
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal("cat", "dog");
    }

    /// <summary>
    /// When the Recycle Bin cannot be used the user is asked again, about deleting for good, and the pet goes if they agree.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_RecycleBinUnavailableAndUserAgrees_DeletesForGood()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");
        _catalog.RecycleFails = true;
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(dog);

        // Assert
        _confirmations.Should().Equal(("dog", false), ("dog", true));
        _catalog.Removals.Should().Equal(("dog", false), ("dog", true));
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal("cat");
    }

    /// <summary>
    /// When the Recycle Bin cannot be used and the user does not want a permanent deletion, the pet stays.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_RecycleBinUnavailableAndUserDeclinesTheSecondQuestion_KeepsThePet()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");
        _catalog.RecycleFails = true;
        _confirmAnswers.Enqueue(true);
        _confirmAnswers.Enqueue(false);
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(dog);

        // Assert
        _catalog.Removals.Should().Equal(("dog", false));
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal("cat", "dog");
    }

    /// <summary>
    /// The pet in use and a pet that comes with the application are not deleted, and the user is not even asked.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_PetInUseOrShippedPet_DoesNothing()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("cat");
        await _settings.UpdateAsync(settings => settings with { PetName = "cat" }, TestContext.Current.CancellationToken);
        await _viewModel.RefreshAsync();

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(_viewModel.Cards.Single(card => card.FolderName == "cat"));
        await _viewModel.DeleteCommand.ExecuteAsync(_viewModel.Cards.Single(card => card.FolderName == "dog"));

        // Assert
        _confirmations.Should().BeEmpty();
        _catalog.Removals.Should().BeEmpty();
    }

    /// <summary>
    /// A pet that could not be loaded can be deleted when it is in the user's folder.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_BrokenPetInUsersFolder_IsDeleted()
    {
        // Arrange
        await CreateAsync("cat", "broken");
        _catalog.Removable.Add("broken");
        await _viewModel.RefreshAsync();
        var broken = _viewModel.Cards.Single(card => card.FolderName == "broken");

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(broken);

        // Assert
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal("cat");
    }

    /// <summary>
    /// A folder that could not be removed is reported and the pet stays in the list.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_RemovalFails_ReportsItAndKeepsThePet()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");
        _catalog.RemoveResult = PetRemoval.Failed;
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(dog);

        // Assert
        _viewModel.PetError.Should().Be(Strings.PetsDeleteFailed);
        _viewModel.Cards.Select(card => card.FolderName).Should().Equal("cat", "dog");
    }

    /// <summary>
    /// A pet that is no longer removable when the removal is tried (the folder is gone from the user's folder) says so.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_NotRemovableAnyMore_ReportsThatItCannotBeDeleted()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");
        _catalog.RemoveResult = PetRemoval.NotRemovable;
        await _viewModel.RefreshAsync();
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(dog);

        // Assert
        _viewModel.PetError.Should().Be(Strings.PetsDeleteShipped);
    }

    /// <summary>
    /// A pet that is deleted is taken off the list of pets that are switched off, so a pet put back under the same name
    /// starts as offered.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_PetWasSwitchedOff_IsTakenOffTheList()
    {
        // Arrange
        await CreateAsync("cat", "dog", "fox");
        _catalog.Removable.Add("dog");
        await _settings.UpdateAsync(settings => settings with { HiddenPets = ["dog", "fox"] }, TestContext.Current.CancellationToken);
        await _viewModel.RefreshAsync();

        // Act
        await _viewModel.DeleteCommand.ExecuteAsync(_viewModel.Cards.Single(card => card.FolderName == "dog"));

        // Assert
        _settings.Current.HiddenPets.Should().Equal("fox");
    }

    /// <summary>
    /// Without a way to ask the user nothing is deleted: a pet is not deleted without asking.
    /// </summary>
    [Fact]
    public async Task DeleteCommand_NoWayToAsk_RemovesNothing()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");
        var tab = new PetsTabViewModel(
            _settings,
            _catalog,
            _loader,
            new PetSwitcher(_settings, _loader, _catalog, _settings.Current.PetName, _ => { }, NullLogger<PetSwitcher>.Instance),
            _ => null);
        await tab.RefreshAsync();

        // Act
        await tab.DeleteCommand.ExecuteAsync(tab.Cards.Single(card => card.FolderName == "dog"));

        // Assert
        _catalog.Removals.Should().BeEmpty();
    }

    /// <summary>
    /// Each row says where its pet comes from: the user's own folder, or the application.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_UsersPetAndShippedPet_ShowTheirOrigin()
    {
        // Arrange
        await CreateAsync("cat", "dog");
        _catalog.Removable.Add("dog");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        var cat = _viewModel.Cards.Single(card => card.FolderName == "cat");
        var dog = _viewModel.Cards.Single(card => card.FolderName == "dog");
        cat.OriginText.Should().Be(Strings.PetsOriginShipped);
        cat.OriginToolTip.Should().Be(Strings.PetsOriginShippedTip);
        dog.OriginText.Should().Be(Strings.PetsOriginUser);
        dog.OriginToolTip.Should().Be(Strings.PetsOriginUserTip);
    }

    /// <summary>
    /// A pet that turns out to be in the user's folder when the list is looked at again (the user put a copy of it there)
    /// gets the label of a pet of the user's, and the card is told, so the row changes without being made anew.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetNowInUsersFolder_OriginChangesOnTheSameCard()
    {
        // Arrange
        await CreateAsync("cat");
        await _viewModel.RefreshAsync();
        var cat = _viewModel.Cards.Single();
        var changed = new List<string?>();
        cat.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _catalog.Removable.Add("cat");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Single().Should().BeSameAs(cat);
        cat.OriginText.Should().Be(Strings.PetsOriginUser);
        changed.Should().Contain([nameof(PetCardViewModel.OriginText), nameof(PetCardViewModel.OriginToolTip)]);
    }

    /// <summary>
    /// A pet that was loaded has the line about the size of its pictures and its states, which every row shows; a pet that
    /// could not be loaded has none.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_LoadedAndBrokenPet_OnlyTheLoadedOneHasInfoText()
    {
        // Arrange
        await CreateAsync("cat", "broken");

        // Act
        await _viewModel.RefreshAsync();

        // Assert
        _viewModel.Cards.Single(card => card.FolderName == "cat").InfoText.Should().NotBeNullOrWhiteSpace();
        _viewModel.Cards.Single(card => card.FolderName == "broken").InfoText.Should().BeEmpty();
    }

    /// <summary>
    /// A catalog that cannot be read.
    /// </summary>
    private sealed class FailingCatalog : IPetCatalog
    {
        /// <inheritdoc />
        public IReadOnlyList<string> GetPetNames()
        {
            throw new IOException("the pets folder cannot be read");
        }

        /// <inheritdoc />
        public string GetPetDirectory(string name)
        {
            return Path.Combine("pets", name);
        }

        /// <inheritdoc />
        public bool CanRemove(string name)
        {
            return false;
        }

        /// <inheritdoc />
        public Task<PetRemoval> RemoveAsync(string name, bool permanently = false)
        {
            return Task.FromResult(PetRemoval.NotRemovable);
        }
    }

    /// <summary>
    /// Creates the view model over fakes.
    /// </summary>
    /// <param name="pets">The pet names the catalog lists; all of them load except one named <c>broken</c>.</param>
    /// <returns>A task that completes when the view model is ready.</returns>
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
            _catalog,
            _settings.Current.PetName,
            _ => { },
            NullLogger<PetSwitcher>.Instance);
        _viewModel = new PetsTabViewModel(
            _settings,
            _catalog,
            _loader,
            switcher,
            _ => _picturesAvailable ? Picture : null,
            names =>
            {
                _preloaded.AddRange(names);
                return _preloadFailure is null ? Task.CompletedTask : Task.FromException(_preloadFailure);
            },
            retain: names => _retained.Add([.. names]),
            confirmDelete: (card, permanently) =>
            {
                _confirmations.Add((card.FolderName, permanently));
                return _confirmAnswers.Count > 0 ? _confirmAnswers.Dequeue() : _confirmAnswer;
            });
    }

    /// <summary>
    /// Waits, briefly, for something that happens after a hand-off to another continuation.
    /// </summary>
    /// <param name="condition">The condition to wait for.</param>
    /// <returns>A task that completes when the condition holds, or after a timeout.</returns>
    private static async Task EventuallyAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
