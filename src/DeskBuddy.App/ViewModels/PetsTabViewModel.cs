using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Rendering;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// Backs the pets tab: lists the installed pets with what each can do. The pet the user clicks is previewed with a
/// moving picture while the others stand still, and only pressing its use button switches the pet.
/// </summary>
public sealed partial class PetsTabViewModel : ObservableObject
{
    /// <summary>
    /// The settings, which store the pet in use.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// Lists the installed pets.
    /// </summary>
    private readonly IPetCatalog _catalog;

    /// <summary>
    /// Loads a pet to show what it is like.
    /// </summary>
    private readonly IPetLoader _loader;

    /// <summary>
    /// The folder the pets are installed in.
    /// </summary>
    private readonly string _petsDirectory;

    /// <summary>
    /// Supplies the pictures of the previews.
    /// </summary>
    private readonly Func<string, ImageSource?> _frames;

    /// <summary>
    /// Cuts the pictures of the previews out ahead of time, off the UI thread; <see langword="null"/> if they are not
    /// prepared ahead.
    /// </summary>
    private readonly Func<IEnumerable<string>, Task>? _preload;

    /// <summary>
    /// Lets one refresh at a time run, as a refresh is slow enough for the user to open the tab again during it.
    /// </summary>
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    /// <summary>
    /// Whether the first look at the pets is over.
    /// </summary>
    private bool _isLoaded;

    /// <summary>
    /// The cards that were loaded, by folder name, so a pet is not loaded again each time the tab is shown.
    /// </summary>
    private Dictionary<string, PetCardViewModel> _known = [];

    /// <summary>
    /// Gets or sets the pets, in the order of their folder names.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<PetCardViewModel> _cards = [];

    /// <summary>
    /// Gets or sets the pet being previewed; <see langword="null"/> if no pet can be shown.
    /// </summary>
    [ObservableProperty]
    private PetCardViewModel? _selectedCard;

    /// <summary>
    /// Gets or sets a problem with changing the pet, or empty.
    /// </summary>
    [ObservableProperty]
    private string _petError = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetsTabViewModel"/> class.
    /// </summary>
    /// <param name="settings">
    /// The settings, which store the pet in use.
    /// </param>
    /// <param name="catalog">
    /// Lists the installed pets.
    /// </param>
    /// <param name="loader">
    /// Loads a pet to show what it is like.
    /// </param>
    /// <param name="petsDirectory">
    /// The folder the pets are installed in.
    /// </param>
    /// <param name="petSwitcher">
    /// Switches the pet, which reports when that fails.
    /// </param>
    /// <param name="frames">
    /// Supplies the pictures of the previews.
    /// </param>
    /// <param name="preload">
    /// Cuts the pictures of the previews out ahead of time on another thread, or <see langword="null"/> to leave that to
    /// <paramref name="frames"/>.
    /// </param>
    public PetsTabViewModel(
        SettingsService settings,
        IPetCatalog catalog,
        IPetLoader loader,
        string petsDirectory,
        PetSwitcher petSwitcher,
        Func<string, ImageSource?> frames,
        Func<IEnumerable<string>, Task>? preload = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentException.ThrowIfNullOrEmpty(petsDirectory);
        ArgumentNullException.ThrowIfNull(petSwitcher);
        ArgumentNullException.ThrowIfNull(frames);

        _settings = settings;
        _catalog = catalog;
        _loader = loader;
        _petsDirectory = petsDirectory;
        _frames = frames;
        _preload = preload;

        _settings.Changed += OnSettingsChanged;
        petSwitcher.SwitchFailed += OnPetSwitchFailed;
    }

    /// <summary>
    /// Gets a value indicating whether the pets have not been looked at yet, so the list is not empty, only not known.
    /// </summary>
    public bool IsLoading => !_isLoaded;

    /// <summary>
    /// Gets a value indicating whether the pets have been looked at and none is installed.
    /// </summary>
    public bool IsEmpty => _isLoaded && Cards.Count == 0;

    /// <summary>
    /// Looks for pets again: new ones appear, removed ones go, and a pet that could not be loaded gets another try.
    /// Pets that were loaded fine, with their pictures, are kept. Call when the tab is shown.
    /// </summary>
    /// <returns>
    /// A task that completes when the list is up to date.
    /// </returns>
    public async Task RefreshAsync()
    {
        await _refreshGate.WaitAsync();
        try
        {
            var cards = new List<PetCardViewModel>();
            // Listing the folders reads the disk, which should not hold up the UI thread.
            foreach (var name in await Task.Run(_catalog.GetPetNames))
            {
                cards.Add(_known.TryGetValue(name, out var existing) && existing.IsAvailable && existing.HasPicture
                    ? existing
                    : await LoadCardAsync(name));
            }

            _known = cards.ToDictionary(card => card.FolderName, StringComparer.Ordinal);
            MarkCurrent(_settings.Current.PetName);
            Cards = cards;
            SelectCard(PickSelection(cards, SelectedCard?.FolderName));
        }
        finally
        {
            _isLoaded = true;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsEmpty));
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Looks at the pets, as <see cref="RefreshAsync"/> does, but a failure is only logged:
    /// it is meant to be started without anything waiting for it, and the panel looks at the pets again when it is opened,
    /// and shows what is wrong then.
    /// </summary>
    /// <param name="logger">
    /// Reports the failure.
    /// </param>
    /// <returns>
    /// A task that completes when the pets have been looked at; it does not fail.
    /// </returns>
    public async Task RefreshQuietlyAsync(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The pets for the panel could not be listed ahead of time.");
        }
    }

    /// <summary>
    /// Moves the preview of the picked pet on; the other pets stand still.
    /// </summary>
    /// <param name="delta">
    /// The time since the previous call.
    /// </param>
    public void Tick(TimeSpan delta)
    {
        SelectedCard?.Tick(delta);
    }

    /// <summary>
    /// Picks a pet to preview. The pet that was picked before goes back to standing still. This does not switch the
    /// pet; see <see cref="UseCommand"/>.
    /// </summary>
    /// <param name="card">
    /// The pet the user clicked; a pet that could not be loaded is ignored.
    /// </param>
    public void SelectCard(PetCardViewModel? card)
    {
        if (card is { IsAvailable: false } || ReferenceEquals(card, SelectedCard))
        {
            return;
        }

        if (SelectedCard is { } previous)
        {
            previous.IsSelected = false;
            previous.Restart();
        }

        SelectedCard = card;
        if (card is not null)
        {
            card.IsSelected = true;
            card.Restart();
        }
    }

    /// <summary>
    /// Puts all previews back on their first picture, so none is left in the middle of a state when the tab comes back.
    /// </summary>
    public void RestartPreviews()
    {
        foreach (var card in Cards)
        {
            card.Restart();
        }
    }

    /// <summary>
    /// Switches to a pet.
    /// </summary>
    /// <param name="card">
    /// The pet whose use button the user pressed.
    /// </param>
    /// <returns>
    /// A task that completes when the choice has been saved.
    /// </returns>
    [RelayCommand]
    private async Task UseAsync(PetCardViewModel? card)
    {
        if (card is not { IsAvailable: true } || card.IsCurrent)
        {
            return;
        }

        PetError = string.Empty;
        try
        {
            await _settings.UpdateAsync(settings => settings with { PetName = card.FolderName });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PetError = string.Format(CultureInfo.CurrentCulture, Strings.SettingsSaveFailed, ex.Message);
        }
        catch (InvalidOperationException)
        {
            // Raised only after a failed load; the panel is disabled then and shows why, so there is nothing to add.
        }
    }

    /// <summary>
    /// Chooses which pet to preview after the list was refreshed: the one that was being previewed if it is still there,
    /// otherwise the pet in use, otherwise the first pet that could be loaded.
    /// </summary>
    /// <param name="cards">
    /// The cards after the refresh.
    /// </param>
    /// <param name="previousName">
    /// The folder name of the pet that was being previewed, if any.
    /// </param>
    /// <returns>
    /// The card to preview, or <see langword="null"/> if no pet could be loaded.
    /// </returns>
    private static PetCardViewModel? PickSelection(IReadOnlyList<PetCardViewModel> cards, string? previousName)
    {
        var available = cards.Where(card => card.IsAvailable).ToList();
        return available.FirstOrDefault(card => card.FolderName == previousName)
            ?? available.FirstOrDefault(card => card.IsCurrent)
            ?? available.FirstOrDefault();
    }

    /// <summary>
    /// Loads a pet for its card; a pet that cannot be loaded gets a card that says why.
    /// </summary>
    /// <param name="name">
    /// The name of the pet's folder.
    /// </param>
    /// <returns>
    /// The card.
    /// </returns>
    private async Task<PetCardViewModel> LoadCardAsync(string name)
    {
        try
        {
            var pet = await _loader.LoadAsync(Path.Combine(_petsDirectory, name));
            if (_preload is not null)
            {
                await _preload(PetShowcase.NativeStatesOf(pet).SelectMany(state => pet.GetAnimation(state).FrameIds));
            }

            return new PetCardViewModel(name, pet, _frames);
        }
        catch (Exception ex) when (PetLoadFailure.Is(ex))
        {
            return new PetCardViewModel(name, ex.Message);
        }
    }

    /// <summary>
    /// Marks the pet in use.
    /// </summary>
    /// <param name="petName">
    /// The folder name of the pet in use.
    /// </param>
    private void MarkCurrent(string petName)
    {
        foreach (var card in _known.Values)
        {
            card.IsCurrent = string.Equals(card.FolderName, petName, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Shows which pet is in use after the settings changed, also when a pet that could not be loaded was put back.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        MarkCurrent(settings.PetName);
    }

    /// <summary>
    /// Shows why a pet could not be switched to.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="reason">
    /// Why the switch failed.
    /// </param>
    private void OnPetSwitchFailed(object? sender, string reason)
    {
        PetError = string.Format(CultureInfo.CurrentCulture, Strings.PetSwitchFailed, reason);
    }
}
