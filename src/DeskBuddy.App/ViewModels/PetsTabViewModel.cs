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
    /// Supplies the pictures of the previews.
    /// </summary>
    private readonly Func<string, ImageSource?> _frames;

    /// <summary>
    /// Cuts the pictures of the previews out ahead of time, off the UI thread; <see langword="null"/> if they are not
    /// prepared ahead.
    /// </summary>
    private readonly Func<IEnumerable<string>, Task>? _preload;

    /// <summary>
    /// Gives back the memory of every picture that is not in the set it is handed; <see langword="null"/> if the pictures
    /// are not held on to.
    /// </summary>
    private readonly Action<IEnumerable<string>>? _retain;

    /// <summary>
    /// Asks whether to delete a pet; <see langword="null"/> if nothing may be deleted.
    /// </summary>
    private readonly Func<PetCardViewModel, bool, bool>? _confirmDelete;

    /// <summary>
    /// Whether the previews are running, which is when the pictures of the picked pet are wanted. While they are not (the
    /// panel is hidden or on another tab) only the pictures the cards stand on are kept, as there are many pets.
    /// </summary>
    private bool _previewsRunning;

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
    /// The name of the interface language the cards in <see cref="_known"/> were made in.
    /// </summary>
    private string _cardsLanguage = Strings.Current.Name;

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
    /// Opens the folder for the user's own pets, or <see langword="null"/> if there is none.
    /// </summary>
    private readonly Action? _openPetsFolder;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetsTabViewModel"/> class.
    /// </summary>
    /// <param name="settings">The settings, which store the pet in use.</param>
    /// <param name="catalog">Lists the installed pets.</param>
    /// <param name="loader">Loads a pet to show what it is like.</param>
    /// <param name="petSwitcher">Switches the pet, which reports when that fails.</param>
    /// <param name="frames">Supplies the pictures of the previews.</param>
    /// <param name="preload">
    /// Cuts the pictures of the previews out ahead of time on another thread, or <see langword="null"/> to leave that to
    /// <paramref name="frames"/>.
    /// </param>
    /// <param name="openPetsFolder">
    /// Opens the folder where the user can put pets of their own, or <see langword="null"/> if there is none.
    /// </param>
    /// <param name="retain">
    /// Forgets the pictures that are not in the set it is given, so that only the picked pet's preview takes memory, or
    /// <see langword="null"/> if the pictures are not held on to.
    /// </param>
    /// <param name="confirmDelete">
    /// Asks the user whether to delete a pet (the card, and whether the question is about deleting for good because the
    /// Recycle Bin cannot be used) and tells the answer; <see langword="null"/> never deletes anything, as a pet is not
    /// deleted without asking.
    /// </param>
    public PetsTabViewModel(
        SettingsService settings,
        IPetCatalog catalog,
        IPetLoader loader,
        PetSwitcher petSwitcher,
        Func<string, ImageSource?> frames,
        Func<IEnumerable<string>, Task>? preload = null,
        Action? openPetsFolder = null,
        Action<IEnumerable<string>>? retain = null,
        Func<PetCardViewModel, bool, bool>? confirmDelete = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(petSwitcher);
        ArgumentNullException.ThrowIfNull(frames);

        _settings = settings;
        _catalog = catalog;
        _loader = loader;
        _frames = frames;
        _preload = preload;
        _openPetsFolder = openPetsFolder;
        _retain = retain;
        _confirmDelete = confirmDelete;

        _settings.Changed += OnSettingsChanged;
        petSwitcher.SwitchFailed += OnPetSwitchFailed;
        LocalizedStrings.Instance.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Makes the cards again after the interface language changed, as they hold the names and the text of the pets in the
    /// language they were made in. The pet being previewed stays picked.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private async void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!_isLoaded)
        {
            // Nothing has been made yet; the first look at the pets writes everything in the new language.
            return;
        }

        try
        {
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing asked for this, so there is no one to tell; the old cards are still in place, and the tab looks at the
            // pets again when it is shown, and reports what is wrong then.
        }
    }

    /// <summary>
    /// Gets a value indicating whether there is a folder for the user's own pets to open.
    /// </summary>
    public bool CanOpenPetsFolder => _openPetsFolder is not null;

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
    /// <returns>A task that completes when the list is up to date.</returns>
    public async Task RefreshAsync()
    {
        await _refreshGate.WaitAsync();
        try
        {
            var cards = new List<PetCardViewModel>();
            // The cards hold text in the language they were made in, so after a change of language none is kept.
            var language = Strings.Current.Name;
            var reusable = string.Equals(language, _cardsLanguage, StringComparison.Ordinal);
            // Listing the folders reads the disk, which should not hold up the UI thread; nor does finding out which of the
            // pets are the user's own, which can be deleted.
            var names = await Task.Run(_catalog.GetPetNames);
            var removable = await Task.Run(() => names.Where(_catalog.CanRemove).ToHashSet(StringComparer.Ordinal));
            foreach (var name in names)
            {
                var known = reusable && _known.TryGetValue(name, out var existing) ? existing : null;
                if (known is { IsAvailable: true, HasPicture: true })
                {
                    cards.Add(known);
                    continue;
                }

                // A pet that did not work is loaded again, in case it has been repaired. If it still fails in the same way
                // the card it had is kept, so that the list is not handed over (and every card built) anew each time.
                var loaded = await LoadCardAsync(name);
                cards.Add(known is not null && IsSameFailure(known, loaded) ? known : loaded);
            }

            _cardsLanguage = language;
            _known = cards.ToDictionary(card => card.FolderName, StringComparer.Ordinal);

            // The cards say what they rest on from here on, so what the previews keep can be worked out (see Retain); the
            // views are told in the finally block.
            _isLoaded = true;
            foreach (var card in cards)
            {
                card.CanDelete = removable.Contains(card.FolderName);
            }

            MarkCurrent(_settings.Current.PetName);
            ApplyHidden(_settings.Current);

            // The same cards in the same order are not handed over again: replacing the list makes the tab build every card
            // anew, which is slow enough to be seen as a flash each time the tab comes into view.
            if (!Cards.SequenceEqual(cards))
            {
                Cards = cards;
            }

            SelectCard(PickSelection(cards, SelectedCard?.FolderName));

            // The picked pet may have stayed the same, in which case SelectCard did nothing; its pictures still have to be
            // ready if the previews are running by now.
            PreparePreview(SelectedCard);
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
    /// <param name="logger">Reports the failure.</param>
    /// <returns>A task that completes when the pets have been looked at; it does not fail.</returns>
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
    /// <param name="delta">The time since the previous call.</param>
    public void Tick(TimeSpan delta)
    {
        SelectedCard?.Tick(delta);
    }

    /// <summary>
    /// Picks a pet to preview. The pet that was picked before goes back to standing still. This does not switch the
    /// pet; see <see cref="UseCommand"/>.
    /// </summary>
    /// <param name="card">The pet the user clicked; a pet that could not be loaded is ignored.</param>
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

        PreparePreview(card);
    }

    /// <summary>
    /// Puts all previews back on their first picture, so none is left in the middle of a state when the tab comes back, and
    /// gets the pictures of the picked pet ready. Call when the previews start running.
    /// </summary>
    public void RestartPreviews()
    {
        _previewsRunning = true;
        foreach (var card in Cards)
        {
            card.Restart();
        }

        PreparePreview(SelectedCard);
    }

    /// <summary>
    /// Gives back the memory of the pictures the previews do not need while they stand still: all that is kept is the
    /// picture each card rests on. Call when the previews stop running.
    /// </summary>
    public void TrimPreviews()
    {
        _previewsRunning = false;
        Retain([]);
    }

    /// <summary>
    /// Keeps the pictures of the picked pet's preview, besides the ones every card rests on, and cuts them out ahead of
    /// time; the pictures of the pet that was picked before are given back.
    /// </summary>
    /// <param name="card">The pet that is previewed, or <see langword="null"/> if there is none.</param>
    private void PreparePreview(PetCardViewModel? card)
    {
        if (!_previewsRunning || card is not { IsAvailable: true })
        {
            return;
        }

        Retain(card.PreviewFrameIds);
        if (_preload is not null)
        {
            _ = PreloadQuietlyAsync(card.PreviewFrameIds);
        }
    }

    /// <summary>
    /// Gives back the memory of every picture except the ones the cards rest on and the given ones. Does nothing until the
    /// first look at the pets is over, as the cards that are still being made have not said what they rest on yet.
    /// </summary>
    /// <param name="extra">The names of the pictures to keep besides those.</param>
    private void Retain(IEnumerable<string> extra)
    {
        if (_retain is null || !_isLoaded)
        {
            return;
        }

        _retain(_known.Values.SelectMany(card => card.RestFrameIds).Concat(extra).ToList());
    }

    /// <summary>
    /// Cuts pictures out ahead of time. Nothing depends on it: a picture that is not ready is cut out when it is asked
    /// for, so a failure here is not worth reporting.
    /// </summary>
    /// <param name="frameIds">The names of the pictures.</param>
    /// <returns>A task that completes when the pictures are ready or the attempt has failed; it does not fail.</returns>
    private async Task PreloadQuietlyAsync(IReadOnlyList<string> frameIds)
    {
        try
        {
            await _preload!(frameIds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Only a head start: the preview cuts the pictures out itself when it needs them.
        }
    }

    /// <summary>
    /// Opens the folder where the user can put pets of their own; they show up the next time the tab is shown.
    /// </summary>
    [RelayCommand]
    private void OpenPetsFolder()
    {
        _openPetsFolder?.Invoke();
    }

    /// <summary>
    /// Switches to a pet.
    /// </summary>
    /// <param name="card">The pet whose use button the user pressed.</param>
    /// <returns>A task that completes when the choice has been saved.</returns>
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
            PetError = string.Format(CultureInfo.CurrentCulture, Strings.SettingsSaveFailed, ErrorText.For(ex));
        }
        catch (InvalidOperationException)
        {
            // Raised only after a failed load; the panel is disabled then and shows why, so there is nothing to add.
        }
    }

    /// <summary>
    /// Switches a pet on or off: whether it is offered when the user changes to another pet. The switch on the card has
    /// already taken the value the user wants.
    /// </summary>
    /// <param name="card">The pet whose switch the user flipped.</param>
    /// <returns>A task that completes when the choice has been saved.</returns>
    [RelayCommand]
    private async Task ToggleShownAsync(PetCardViewModel? card)
    {
        if (card is not { CanChangeShown: true })
        {
            return;
        }

        var offered = card.IsShown;
        PetError = string.Empty;
        try
        {
            await _settings.UpdateAsync(settings => settings with { HiddenPets = WithHidden(settings.HiddenPets, card.FolderName, hidden: !offered) });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PetError = string.Format(CultureInfo.CurrentCulture, Strings.SettingsSaveFailed, ErrorText.For(ex));
        }
        finally
        {
            // Whatever came of it, the switch shows what the settings say (it already took the user's value).
            ApplyHidden(_settings.Current);
        }
    }

    /// <summary>
    /// Deletes a pet after asking: its folder goes to the Recycle Bin, or, if that cannot be used and the user agrees, is
    /// deleted for good. Only a pet in the user's own folder can be deleted, and not the pet in use.
    /// </summary>
    /// <param name="card">The pet whose delete button the user pressed.</param>
    /// <returns>A task that completes when the pet is deleted, or it is clear that it is not to be.</returns>
    [RelayCommand]
    private async Task DeleteAsync(PetCardViewModel? card)
    {
        if (card is not { CanDeleteNow: true } || _confirmDelete is null)
        {
            return;
        }

        PetError = string.Empty;
        if (!_confirmDelete(card, false))
        {
            return;
        }

        var result = await _catalog.RemoveAsync(card.FolderName);
        if (result == PetRemoval.RecycleUnavailable)
        {
            if (!_confirmDelete(card, true))
            {
                return;
            }

            result = await _catalog.RemoveAsync(card.FolderName, permanently: true);
        }

        switch (result)
        {
            case PetRemoval.Removed:
                await ForgetHiddenAsync(card.FolderName);

                // The card is dropped so that the refresh loads the pet again: a shipped pet the deleted copy stood in for
                // comes back under the same name, and the card of the copy would be reused for it.
                _known.Remove(card.FolderName);
                await RefreshAsync();
                break;
            case PetRemoval.NotRemovable:
                PetError = Strings.PetsDeleteShipped;
                break;
            default:
                PetError = Strings.PetsDeleteFailed;
                break;
        }
    }

    /// <summary>
    /// Takes a pet that has been deleted out of the list of pets that are switched off, so that a pet that is put back
    /// under the same name starts as offered.
    /// </summary>
    /// <param name="name">The folder name of the pet.</param>
    /// <returns>A task that completes when the settings have been saved.</returns>
    private async Task ForgetHiddenAsync(string name)
    {
        if (!_settings.Current.HiddenPets.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await _settings.UpdateAsync(settings => settings with { HiddenPets = WithHidden(settings.HiddenPets, name, hidden: false) });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PetError = string.Format(CultureInfo.CurrentCulture, Strings.SettingsSaveFailed, ErrorText.For(ex));
        }
    }

    /// <summary>
    /// Adds a pet to the list of pets that are switched off, or takes it out.
    /// </summary>
    /// <param name="hiddenPets">The list as it is.</param>
    /// <param name="name">The folder name of the pet.</param>
    /// <param name="hidden">Whether the pet is to be on the list.</param>
    /// <returns>The new list.</returns>
    private static string[] WithHidden(IReadOnlyList<string> hiddenPets, string name, bool hidden)
    {
        var others = hiddenPets.Where(entry => !string.Equals(entry, name, StringComparison.OrdinalIgnoreCase));
        return hidden ? [.. others, name] : [.. others];
    }

    /// <summary>
    /// Shows which pets are offered, as the settings say; the pet in use always is.
    /// </summary>
    /// <param name="settings">The settings.</param>
    private void ApplyHidden(PetSettings settings)
    {
        foreach (var card in _known.Values)
        {
            card.IsShown = card.IsCurrent || !settings.HiddenPets.Contains(card.FolderName, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Tells whether two cards of a pet that does not work are the same: they have no picture, and say the same.
    /// </summary>
    /// <param name="earlier">The card the tab had.</param>
    /// <param name="later">The card made by loading the pet again.</param>
    /// <returns><see langword="true"/> if nothing about the pet has changed for the user.</returns>
    private static bool IsSameFailure(PetCardViewModel earlier, PetCardViewModel later)
    {
        return !earlier.HasPicture
            && !later.HasPicture
            && earlier.IsAvailable == later.IsAvailable
            && string.Equals(earlier.LoadError, later.LoadError, StringComparison.Ordinal)
            && string.Equals(earlier.DisplayName, later.DisplayName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Chooses which pet to preview after the list was refreshed: the one that was being previewed if it is still there,
    /// otherwise the pet in use, otherwise the first pet that could be loaded.
    /// </summary>
    /// <param name="cards">The cards after the refresh.</param>
    /// <param name="previousName">The folder name of the pet that was being previewed, if any.</param>
    /// <returns>The card to preview, or <see langword="null"/> if no pet could be loaded.</returns>
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
    /// <param name="name">The name of the pet's folder.</param>
    /// <returns>The card.</returns>
    private async Task<PetCardViewModel> LoadCardAsync(string name)
    {
        try
        {
            var pet = await _loader.LoadAsync(_catalog.GetPetDirectory(name));
            if (_preload is not null)
            {
                // Only what the card shows while it stands still: all pets are listed at once, and the pictures of the
                // rest of the states are cut out when the pet is picked (see PreparePreview).
                await _preload(pet.GetAnimation(PetState.Idle).FrameIds);
            }

            return new PetCardViewModel(name, pet, _frames);
        }
        catch (Exception ex) when (PetLoadFailure.Is(ex))
        {
            return new PetCardViewModel(name, ErrorText.For(ex));
        }
    }

    /// <summary>
    /// Marks the pet in use.
    /// </summary>
    /// <param name="petName">The folder name of the pet in use.</param>
    private void MarkCurrent(string petName)
    {
        foreach (var card in _known.Values)
        {
            card.IsCurrent = string.Equals(card.FolderName, petName, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Shows which pet is in use and which pets are switched off after the settings changed, also when a pet that could not
    /// be loaded was put back.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="settings">The new settings.</param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        MarkCurrent(settings.PetName);
        ApplyHidden(settings);
    }

    /// <summary>
    /// Shows why a pet could not be switched to.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="reason">Why the switch failed.</param>
    private void OnPetSwitchFailed(object? sender, string reason)
    {
        PetError = string.Format(CultureInfo.CurrentCulture, Strings.PetSwitchFailed, reason);
    }
}
