using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// Backs the settings tab. Every change takes effect and is saved at once: choices when they are made, and text
/// entries when the user leaves the box or presses Enter. A value that is not valid is not applied; the tab says what is
/// expected instead of quietly correcting it.
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

        Refresh(settings.Current);
        RefreshExternalState();

        _settings.Changed += OnSettingsChanged;
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
    /// Changes the settings and shows a failed save.
    /// </summary>
    /// <param name="change">
    /// Produces the new settings from the current ones.
    /// </param>
    /// <returns>
    /// A task that completes when the change has been applied and saved.
    /// </returns>
    private async Task ApplyAsync(Func<PetSettings, PetSettings> change)
    {
        try
        {
            await _settings.UpdateAsync(change);
            SaveError = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = string.Format(CultureInfo.CurrentCulture, Strings.SettingsSaveFailed, ex.Message);
        }
        catch (InvalidOperationException)
        {
            // Raised only after a failed load; the tab is disabled then and shows why, so there is nothing to add.
        }
    }

    /// <summary>
    /// Shows settings that were changed elsewhere, such as a boss key that was put back.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        Refresh(settings);
    }

    /// <summary>
    /// Fills the properties from the settings without treating that as an edit.
    /// </summary>
    /// <param name="settings">
    /// The settings to show.
    /// </param>
    private void Refresh(PetSettings settings)
    {
        _refreshing = true;
        try
        {
            ChatterEnabled = settings.ChatterEnabled;
            SedentaryText = Minutes(settings.SedentaryReminderAfter);
            SleepText = Minutes(settings.SleepAfter);
            BossKeyText = settings.BossKey.ToString();
            AutoHideWhenFullscreen = settings.AutoHideWhenFullscreen;
            DetectCoveredMonitor = settings.DetectCoveredMonitor;
            ShowProcessList(ProcessListKind.Fullscreen, settings);
            ShowProcessList(ProcessListKind.Development, settings);
            ShowProcessList(ProcessListKind.Meeting, settings);
            ShowProcessList(ProcessListKind.Chat, settings);
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

            TimingError = string.Empty;
        }
        finally
        {
            _refreshing = false;
        }
    }
}
