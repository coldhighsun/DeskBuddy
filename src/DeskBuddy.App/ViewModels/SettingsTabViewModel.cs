using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// Backs the settings tabs. Changes only edit a draft held by a <see cref="SettingsEditor"/>; they take effect and are
/// saved together when the user saves, and are thrown away when the user reverts. A value that is not valid blocks
/// saving; the tab says what is expected instead of quietly correcting it.
/// </summary>
public sealed partial class SettingsTabViewModel : ObservableObject
{
    /// <summary>
    /// The settings being edited.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// The installed pets, whose pictures show what each size looks like; <see langword="null"/> if there are none to show.
    /// </summary>
    private readonly InstalledPets? _pets;

    /// <summary>
    /// The draft of the settings the user is editing.
    /// </summary>
    private readonly SettingsEditor _editor;

    /// <summary>
    /// Whether the properties are being filled from the settings, in which case changing them must not be treated as
    /// the user's edit.
    /// </summary>
    private bool _refreshing;

    /// <summary>
    /// A problem with saving the settings, or empty.
    /// </summary>
    [ObservableProperty]
    private string _saveError = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsTabViewModel"/> class and shows the current settings.
    /// </summary>
    /// <param name="settings">
    /// The settings being edited.
    /// </param>
    /// <param name="bossKey">
    /// The boss key registration.
    /// </param>
    /// <param name="autoStart">
    /// Starts the application at sign-in.
    /// </param>
    /// <param name="openLogDirectory">
    /// Opens the log directory in the file manager.
    /// </param>
    /// <param name="pets">
    /// The installed pets, whose pictures show what each size looks like; <see langword="null"/> to show the sizes by
    /// their names only.
    /// </param>
    public SettingsTabViewModel(
        SettingsService settings,
        BossKeyController bossKey,
        IAutoStartService autoStart,
        Action openLogDirectory,
        InstalledPets? pets = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(bossKey);
        ArgumentNullException.ThrowIfNull(autoStart);
        ArgumentNullException.ThrowIfNull(openLogDirectory);

        _openLogDirectory = openLogDirectory;
        _settings = settings;
        _bossKey = bossKey;
        _autoStartService = autoStart;
        _pets = pets;

        _editor = new SettingsEditor(settings);

        _processLists = new Dictionary<ProcessListKind, ProcessListBox>
        {
            [ProcessListKind.Fullscreen] = new(() => BlacklistText, text => BlacklistText = text, () => BlacklistError, error => BlacklistError = error),
            [ProcessListKind.Development] = new(() => DevProcessesText, text => DevProcessesText = text, () => DevProcessesError, error => DevProcessesError = error),
            [ProcessListKind.Meeting] = new(() => MeetingProcessesText, text => MeetingProcessesText = text, () => MeetingProcessesError, error => MeetingProcessesError = error),
            [ProcessListKind.Chat] = new(() => ChatProcessesText, text => ChatProcessesText = text, () => ChatProcessesError, error => ChatProcessesError = error),
        };

        Scenarios =
        [
            .. ScenarioCatalog.All.Select(state => new ScenarioToggleViewModel(state, Strings.ScenarioLabel(state), OnScenarioToggled)),
        ];

        _removedAtLoad = settings.RemovedAtLoad;
        UpdateProcessListNotice();

        Refresh(_editor.Draft);
        RefreshExternalState();

        _settings.Changed += OnSettingsChanged;
        _editor.Changed += OnEditorChanged;
        _bossKey.StatusChanged += OnBossKeyStatusChanged;
        if (_pets is not null)
        {
            _pets.Changed += OnPetsChanged;
        }

        if (_settings.LoadFailure is { } failure)
        {
            LoadFailureMessage = string.Format(CultureInfo.CurrentCulture, Strings.SettingsLoadFailed, failure.Message);
        }

        if (_bossKey.RegistrationError is { } reason)
        {
            BossKeyError = string.Format(CultureInfo.CurrentCulture, Strings.BossKeyFailed, settings.Current.BossKey, reason);
        }
    }

    /// <summary>
    /// Gets a value indicating whether settings can be changed (they cannot if loading failed at startup).
    /// </summary>
    public bool IsEditable => _settings.LoadFailure is null;

    /// <summary>
    /// Gets why settings cannot be changed, or empty if they can.
    /// </summary>
    public string LoadFailureMessage { get; } = string.Empty;


    /// <summary>
    /// Gets a value indicating whether something was changed and not saved yet. An entry that cannot be saved counts too,
    /// as it is still waiting to be fixed or reverted.
    /// </summary>
    public bool HasUnsavedChanges => _editor.IsDirty || _editor.HasErrors || AutoStart != _autoStartSaved;

    /// <summary>
    /// Gets a value indicating whether saving is possible: there is a change, nothing in it is invalid, and the saved
    /// settings were read at startup.
    /// </summary>
    public bool CanSave => IsEditable && !_editor.HasErrors && HasUnsavedChanges;

    /// <summary>
    /// Saves what was changed. The start-at-sign-in switch goes first because it lives outside the settings; the boss key
    /// is the other entry the system may refuse, in which case it goes back to the shortcut that works and the rest is
    /// still saved.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if everything was saved; <see langword="false"/> if there was nothing to save, an entry is
    /// invalid, or a part could not be applied (the tab says which).
    /// </returns>
    public async Task<bool> TrySaveAsync()
    {
        if (!CanSave)
        {
            return false;
        }

        var succeeded = SaveAutoStart();
        var bossKey = _editor.Draft.BossKey;
        var previousBossKey = _editor.Baseline.BossKey;
        var bossKeyChanged = bossKey != previousBossKey;

        _applyingBossKey = true;
        try
        {
            if (_editor.IsDirty)
            {
                succeeded &= await _editor.SaveAsync();
                ShowSaveFailure();

                if (bossKeyChanged && _bossKey.RegistrationError is { } reason)
                {
                    succeeded = false;
                    var working = _bossKey.Registered ?? previousBossKey;
                    _editor.Set(BossKeyEditKey, settings => settings with { BossKey = working });
                    await _editor.SaveAsync();
                    ShowSaveFailure();
                    BossKeyError = string.Format(CultureInfo.CurrentCulture, Strings.BossKeyFailed, bossKey, reason);
                }
                else if (bossKeyChanged)
                {
                    BossKeyError = string.Empty;
                }
            }
        }
        finally
        {
            _applyingBossKey = false;
        }

        Refresh(_editor.Draft);
        NotifyUnsavedChanged();
        return succeeded;
    }

    /// <summary>
    /// Shows why the last write of the settings failed, or clears the message if it did not fail.
    /// </summary>
    private void ShowSaveFailure()
    {
        SaveError = _editor.SaveFailure is { } failure
            ? string.Format(CultureInfo.CurrentCulture, Strings.SettingsSaveFailed, failure.Message)
            : string.Empty;
    }

    /// <summary>
    /// Throws away what was changed and shows the saved settings again.
    /// </summary>
    [RelayCommand]
    public void Revert()
    {
        _editor.Revert();
        _refreshing = true;
        try
        {
            AutoStart = _autoStartSaved;
        }
        finally
        {
            _refreshing = false;
        }

        BossKeyError = string.Empty;
        AutoStartError = string.Empty;
        SaveError = string.Empty;
        foreach (var kind in ProcessListRules.All)
        {
            _processLists[kind].SetError(string.Empty);
        }

        _editedLists.Clear();
        UpdateProcessListNotice();

        Refresh(_editor.Draft);
        NotifyUnsavedChanged();
    }

    /// <summary>
    /// Saves from the save button.
    /// </summary>
    /// <returns>
    /// A task that completes when saving is done.
    /// </returns>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync()
    {
        return TrySaveAsync();
    }

    /// <summary>
    /// Shows settings that were changed elsewhere, such as the size chosen in the tray menu. Entries the user has not
    /// edited follow along; the ones they edited keep their value.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        Refresh(_editor.Draft, keepTypedText: true);
    }

    /// <summary>
    /// Tells the tab that what can be saved may have changed.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// The event data.
    /// </param>
    private void OnEditorChanged(object? sender, EventArgs e)
    {
        NotifyUnsavedChanged();
    }

    /// <summary>
    /// Raises the change notifications for the unsaved state and updates the save button.
    /// </summary>
    private void NotifyUnsavedChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Fills the properties from the draft without treating that as an edit. A box holding an entry that is not valid
    /// keeps what the user typed.
    /// </summary>
    /// <param name="settings">
    /// The settings to show.
    /// </param>
    /// <param name="keepTypedText">
    /// Whether a text box whose text already means the value to show is left alone, so a change made elsewhere does not
    /// rewrite what the user is typing; after saving or reverting the boxes show the cleaned-up values instead.
    /// </param>
    private void Refresh(PetSettings settings, bool keepTypedText = false)
    {
        _refreshing = true;
        try
        {
            ChatterEnabled = settings.ChatterEnabled;
            if (_editor.ErrorFor(SedentaryEditKey).Length == 0 && !(keepTypedText && MeansMinutes(SedentaryText, settings.SedentaryReminderAfter)))
            {
                SedentaryText = Minutes(settings.SedentaryReminderAfter);
            }

            if (_editor.ErrorFor(SleepEditKey).Length == 0 && !(keepTypedText && MeansMinutes(SleepText, settings.SleepAfter)))
            {
                SleepText = Minutes(settings.SleepAfter);
            }

            BossKeyText = settings.BossKey.ToString();
            AutoHideWhenFullscreen = settings.AutoHideWhenFullscreen;
            DetectCoveredMonitor = settings.DetectCoveredMonitor;
            ShowProcessList(ProcessListKind.Fullscreen, settings, keepTypedText);
            ShowProcessList(ProcessListKind.Development, settings, keepTypedText);
            ShowProcessList(ProcessListKind.Meeting, settings, keepTypedText);
            ShowProcessList(ProcessListKind.Chat, settings, keepTypedText);
            ScenariosEnabled = settings.ScenariosEnabled;
            SelectedLanguage = settings.Language;
            SelectedTheme = settings.Theme;
            SelectedSize = settings.Size;
            UpdateSizeChoices(settings.PetName);
            ClimbEdges = settings.ClimbEdges;
            ClockOutHour = settings.ClockOutTime.Hours;
            var minuteChoices = ClockOutMinuteChoices(settings.ClockOutTime.Minutes);
            if (!minuteChoices.SequenceEqual(MinuteChoices))
            {
                // Replacing the list clears the selection of the box showing it, so only do it when it really changed.
                MinuteChoices = minuteChoices;
            }

            ClockOutMinute = settings.ClockOutTime.Minutes;
            OnPropertyChanged(nameof(ClockOutMinute));
            foreach (var toggle in Scenarios)
            {
                toggle.Show(!settings.DisabledScenarios.Contains(toggle.State.ToString(), StringComparer.OrdinalIgnoreCase));
            }

            UpdateTimingError();
        }
        finally
        {
            _refreshing = false;
        }
    }
}
