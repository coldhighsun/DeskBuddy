using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.App.Resources;
using DeskBuddy.Application;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// Backs the settings tab. Every change takes effect and is saved at once: choices when they are made, and text
/// entries when the user leaves the box or presses Enter. A value that is not valid is not applied; the tab says what is
/// expected instead of quietly correcting it.
/// </summary>
public sealed partial class SettingsTabViewModel : ObservableObject
{
    /// <summary>
    /// Shortest value accepted for a time setting, in minutes.
    /// </summary>
    private const int MinMinutes = 1;

    /// <summary>
    /// The ways a time of day may be written: <c>18:00</c> or <c>8:30</c>.
    /// </summary>
    private static readonly string[] ClockOutTimeFormats = [@"h\:mm", @"hh\:mm"];

    /// <summary>
    /// The settings being edited.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// The boss key registration, to find out whether a new shortcut could be registered.
    /// </summary>
    private readonly BossKeyController _bossKey;

    /// <summary>
    /// Starts the application at sign-in.
    /// </summary>
    private readonly IAutoStartService _autoStartService;

    /// <summary>
    /// Opens the directory the log files are written to.
    /// </summary>
    private readonly Action _openLogDirectory;

    /// <summary>
    /// Whether the properties are being filled from the settings, in which case changing them must not be treated as
    /// the user's edit.
    /// </summary>
    private bool _refreshing;

    /// <summary>
    /// Whether a new boss key is being tried, during which the controller's status changes are handled by that attempt.
    /// </summary>
    private bool _applyingBossKey;

    /// <summary>
    /// Whether the pet stays on the taskbar.
    /// </summary>
    [ObservableProperty]
    private bool _isDocked;

    /// <summary>
    /// Whether the pet roams the desktop.
    /// </summary>
    [ObservableProperty]
    private bool _isRoam;

    /// <summary>
    /// The sedentary reminder interval in minutes, as typed.
    /// </summary>
    [ObservableProperty]
    private string _sedentaryText = string.Empty;

    /// <summary>
    /// The time before the pet falls asleep in minutes, as typed.
    /// </summary>
    [ObservableProperty]
    private string _sleepText = string.Empty;

    /// <summary>
    /// The boss key as text, for example <c>Ctrl+Alt+H</c>.
    /// </summary>
    [ObservableProperty]
    private string _bossKeyText = string.Empty;

    /// <summary>
    /// Whether the pet hides for full-screen applications.
    /// </summary>
    [ObservableProperty]
    private bool _autoHideWhenFullscreen;

    /// <summary>
    /// Whether a window covering a whole monitor counts as full screen.
    /// </summary>
    [ObservableProperty]
    private bool _detectCoveredMonitor;

    /// <summary>
    /// The blacklisted process names, one per line.
    /// </summary>
    [ObservableProperty]
    private string _blacklistText = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the pet sits down at its computer while the user is developing.
    /// </summary>
    [ObservableProperty]
    private bool _devModeEnabled;

    /// <summary>
    /// Gets or sets the development tools, one process name per line.
    /// </summary>
    [ObservableProperty]
    private string _devProcessesText = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the pet acts out situations by itself.
    /// </summary>
    [ObservableProperty]
    private bool _scenariosEnabled;

    /// <summary>
    /// Gets or sets the meeting applications, one process name per line.
    /// </summary>
    [ObservableProperty]
    private string _meetingProcessesText = string.Empty;

    /// <summary>
    /// Gets or sets the explanation shown when the clock-out time is not a time of day; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _clockOutTimeError = string.Empty;

    /// <summary>
    /// Gets or sets the language of the user interface.
    /// </summary>
    [ObservableProperty]
    private AppLanguage _selectedLanguage;

    /// <summary>
    /// Gets or sets the color theme of the user interface.
    /// </summary>
    [ObservableProperty]
    private AppTheme _selectedTheme;

    /// <summary>
    /// Gets or sets how large the pet is shown.
    /// </summary>
    [ObservableProperty]
    private PetSize _selectedSize;

    /// <summary>
    /// Gets or sets a value indicating whether the pet may climb the sides of the screen and hang from its top edge.
    /// </summary>
    [ObservableProperty]
    private bool _climbEdges;

    /// <summary>
    /// Gets or sets the time of day the working day ends, as <c>HH:mm</c>.
    /// </summary>
    [ObservableProperty]
    private string _clockOutTimeText = string.Empty;

    /// <summary>
    /// Whether the application starts at sign-in.
    /// </summary>
    [ObservableProperty]
    private bool _autoStart;

    /// <summary>
    /// A problem with the last timing entry, or empty.
    /// </summary>
    [ObservableProperty]
    private string _timingError = string.Empty;

    /// <summary>
    /// A problem with the boss key, or empty.
    /// </summary>
    [ObservableProperty]
    private string _bossKeyError = string.Empty;

    /// <summary>
    /// A problem with the start-at-sign-in switch, or empty.
    /// </summary>
    [ObservableProperty]
    private string _autoStartError = string.Empty;

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
    public SettingsTabViewModel(
        SettingsService settings,
        BossKeyController bossKey,
        IAutoStartService autoStart,
        Action openLogDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(bossKey);
        ArgumentNullException.ThrowIfNull(autoStart);
        ArgumentNullException.ThrowIfNull(openLogDirectory);

        _openLogDirectory = openLogDirectory;
        _settings = settings;
        _bossKey = bossKey;
        _autoStartService = autoStart;

        Scenarios =
        [
            .. ScenarioCatalog.All.Select(state => new ScenarioToggleViewModel(state, Strings.ScenarioLabel(state), OnScenarioToggled)),
        ];

        Refresh(settings.Current);
        RefreshExternalState();

        _settings.Changed += OnSettingsChanged;
        _bossKey.StatusChanged += OnBossKeyStatusChanged;

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
    /// Gets the switches for the individual scenarios.
    /// </summary>
    public IReadOnlyList<ScenarioToggleViewModel> Scenarios { get; }

    /// <summary>
    /// Gets a value indicating whether settings can be changed (they cannot if loading failed at startup).
    /// </summary>
    public bool IsEditable => _settings.LoadFailure is null;

    /// <summary>
    /// Opens the log directory in the file manager.
    /// </summary>
    [RelayCommand]
    private void OpenLogs()
    {
        _openLogDirectory();
    }

    /// <summary>
    /// Gets why settings cannot be changed, or empty if they can.
    /// </summary>
    public string LoadFailureMessage { get; } = string.Empty;

    /// <summary>
    /// Re-reads what can change without the application knowing: whether the application is registered to start at
    /// sign-in (the user can switch that off in Task Manager). Call when the tab is shown.
    /// </summary>
    public void RefreshExternalState()
    {
        _refreshing = true;
        try
        {
            AutoStart = _autoStartService.IsEnabled();
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Tries a new boss key. If the system refuses it (another program uses it) the setting goes back to the shortcut
    /// that works, so the settings always name the shortcut that is active, and the reason is shown.
    /// </summary>
    /// <param name="gesture">
    /// The shortcut the user entered.
    /// </param>
    /// <returns>
    /// A task that completes when the shortcut has been applied or rejected.
    /// </returns>
    public async Task ApplyBossKeyAsync(HotkeyGesture gesture)
    {
        var previous = _settings.Current.BossKey;
        if (gesture == previous)
        {
            BossKeyError = string.Empty;
            return;
        }

        _applyingBossKey = true;
        try
        {
            await ApplyAsync(settings => settings with { BossKey = gesture });
            if (_bossKey.RegistrationError is { } reason)
            {
                var working = _bossKey.Registered ?? previous;
                await ApplyAsync(settings => settings with { BossKey = working });
                BossKeyError = string.Format(CultureInfo.CurrentCulture, Strings.BossKeyFailed, gesture, reason);
            }
            else
            {
                BossKeyError = string.Empty;
            }
        }
        finally
        {
            _applyingBossKey = false;
        }
    }

    /// <summary>
    /// Tells the user that the keys they pressed cannot be used as the boss key.
    /// </summary>
    public void ReportBossKeyRejected()
    {
        BossKeyError = Strings.BossKeyInvalid;
    }

    /// <summary>
    /// Applies the docked mode when it is chosen.
    /// </summary>
    /// <param name="value">
    /// Whether the docked mode is chosen.
    /// </param>
    partial void OnIsDockedChanged(bool value)
    {
        if (!_refreshing && value)
        {
            _ = ApplyAsync(settings => settings with { Mode = DisturbanceMode.Docked });
        }
    }

    /// <summary>
    /// Applies the roaming mode when it is chosen.
    /// </summary>
    /// <param name="value">
    /// Whether the roaming mode is chosen.
    /// </param>
    partial void OnIsRoamChanged(bool value)
    {
        if (!_refreshing && value)
        {
            _ = ApplyAsync(settings => settings with { Mode = DisturbanceMode.Roam });
        }
    }

    /// <summary>
    /// Applies a typed sedentary reminder interval.
    /// </summary>
    /// <param name="value">
    /// The text entered.
    /// </param>
    partial void OnSedentaryTextChanged(string value)
    {
        CommitMinutes(
            value,
            Strings.SettingsSedentary,
            MinMinutes,
            (int)PetSettings.MaxSedentaryInterval.TotalMinutes,
            (settings, minutes) => settings with { SedentaryReminderAfter = minutes });
    }

    /// <summary>
    /// Applies a typed time before the pet falls asleep.
    /// </summary>
    /// <param name="value">
    /// The text entered.
    /// </param>
    partial void OnSleepTextChanged(string value)
    {
        CommitMinutes(
            value,
            Strings.SettingsSleep,
            MinMinutes,
            (int)PetSettings.MaxSleepAfter.TotalMinutes,
            (settings, minutes) => settings with { SleepAfter = minutes });
    }

    /// <summary>
    /// Applies the full-screen auto-hide choice.
    /// </summary>
    /// <param name="value">
    /// Whether the pet hides for full-screen applications.
    /// </param>
    partial void OnAutoHideWhenFullscreenChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { AutoHideWhenFullscreen = value });
        }
    }

    /// <summary>
    /// Applies the monitor-cover detection choice.
    /// </summary>
    /// <param name="value">
    /// Whether a window covering a monitor counts as full screen.
    /// </param>
    partial void OnDetectCoveredMonitorChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { DetectCoveredMonitor = value });
        }
    }

    /// <summary>
    /// Applies the edited process list.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnBlacklistTextChanged(string value)
    {
        if (_refreshing)
        {
            return;
        }

        var names = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        _ = ApplyAsync(settings => settings with { BlacklistedProcesses = names });
    }

    /// <summary>
    /// Applies the development detection choice.
    /// </summary>
    /// <param name="value">
    /// Whether the pet sits down at its computer while the user is developing.
    /// </param>
    partial void OnDevModeEnabledChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { DevModeEnabled = value });
        }
    }

    /// <summary>
    /// Applies the choice to let the pet act out situations by itself.
    /// </summary>
    /// <param name="value">
    /// Whether scenarios are on.
    /// </param>
    partial void OnScenariosEnabledChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { ScenariosEnabled = value });
        }
    }

    /// <summary>
    /// Applies the edited list of meeting applications.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnMeetingProcessesTextChanged(string value)
    {
        if (_refreshing)
        {
            return;
        }

        var names = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        _ = ApplyAsync(settings => settings with { MeetingProcesses = names });
    }

    /// <summary>
    /// Gets the languages to choose from.
    /// </summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new(AppLanguage.Auto, Strings.LanguageAuto),
        new(AppLanguage.ZhCN, Strings.LanguageZhCN),
        new(AppLanguage.En, Strings.LanguageEn),
    ];

    /// <summary>
    /// Gets the themes to choose from.
    /// </summary>
    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new(AppTheme.Auto, Strings.ThemeAuto),
        new(AppTheme.Light, Strings.ThemeLight),
        new(AppTheme.Dark, Strings.ThemeDark),
    ];

    /// <summary>
    /// Gets the pet sizes to choose from.
    /// </summary>
    public IReadOnlyList<PetSizeOption> Sizes { get; } =
    [
        new(PetSize.Small, Strings.PetSizeSmall),
        new(PetSize.Medium, Strings.PetSizeMedium),
        new(PetSize.Large, Strings.PetSizeLarge),
    ];

    /// <summary>
    /// Applies the chosen theme; it takes effect at once.
    /// </summary>
    /// <param name="value">
    /// The theme.
    /// </param>
    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { Theme = value });
        }
    }

    /// <summary>
    /// Applies the chosen pet size.
    /// </summary>
    /// <param name="value">
    /// The size.
    /// </param>
    partial void OnSelectedSizeChanged(PetSize value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { Size = value });
        }
    }

    /// <summary>
    /// Applies the choice to let the pet climb along the edges of the screen.
    /// </summary>
    /// <param name="value">
    /// Whether the pet may climb along the edges.
    /// </param>
    partial void OnClimbEdgesChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { ClimbEdges = value });
        }
    }

    /// <summary>
    /// Applies the chosen language; it takes full effect after a restart.
    /// </summary>
    /// <param name="value">
    /// The language.
    /// </param>
    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { Language = value });
        }
    }

    /// <summary>
    /// Applies the edited time at which the working day ends. Text that is not a time of day is not applied and is
    /// explained until it becomes one.
    /// </summary>
    /// <param name="value">
    /// The text entered, such as <c>18:00</c>.
    /// </param>
    partial void OnClockOutTimeTextChanged(string value)
    {
        if (_refreshing)
        {
            return;
        }

        if (!TimeSpan.TryParseExact(value.Trim(), ClockOutTimeFormats, CultureInfo.InvariantCulture, out var time)
            || time >= TimeSpan.FromHours(24))
        {
            ClockOutTimeError = Strings.ClockOutTimeInvalid;
            return;
        }

        ClockOutTimeError = string.Empty;
        _ = ApplyAsync(settings => settings with { ClockOutTime = time });
    }

    /// <summary>
    /// Applies the switch of a single scenario.
    /// </summary>
    /// <param name="state">
    /// The scenario.
    /// </param>
    /// <param name="enabled">
    /// Whether it is on.
    /// </param>
    private void OnScenarioToggled(PetState state, bool enabled)
    {
        if (_refreshing)
        {
            return;
        }

        var name = state.ToString();
        _ = ApplyAsync(settings => settings with
        {
            DisabledScenarios =
            [
                .. settings.DisabledScenarios.Where(disabled => !string.Equals(disabled, name, StringComparison.OrdinalIgnoreCase)),
                .. enabled ? [] : new[] { name },
            ],
        });
    }

    /// <summary>
    /// Applies the edited list of development tools.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnDevProcessesTextChanged(string value)
    {
        if (_refreshing)
        {
            return;
        }

        var names = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        _ = ApplyAsync(settings => settings with { DevProcesses = names });
    }

    /// <summary>
    /// Switches start at sign-in on or off. If the system does not allow it the switch goes back and the reason is
    /// shown.
    /// </summary>
    /// <param name="value">
    /// Whether the application should start at sign-in.
    /// </param>
    partial void OnAutoStartChanged(bool value)
    {
        if (_refreshing)
        {
            return;
        }

        if (_autoStartService.TrySet(value, out var error))
        {
            AutoStartError = string.Empty;
            return;
        }

        AutoStartError = string.Format(CultureInfo.CurrentCulture, Strings.AutoStartFailed, error);
        _refreshing = true;
        try
        {
            AutoStart = _autoStartService.IsEnabled();
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Validates a number of minutes and applies it, or says what is expected.
    /// </summary>
    /// <param name="text">
    /// The text entered.
    /// </param>
    /// <param name="label">
    /// The name of the entry, for the message.
    /// </param>
    /// <param name="min">
    /// The smallest allowed number of minutes.
    /// </param>
    /// <param name="max">
    /// The largest allowed number of minutes.
    /// </param>
    /// <param name="apply">
    /// Puts the duration into the settings.
    /// </param>
    private void CommitMinutes(
        string text,
        string label,
        int min,
        int max,
        Func<PetSettings, TimeSpan, PetSettings> apply)
    {
        if (_refreshing)
        {
            return;
        }

        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || minutes < min
            || minutes > max)
        {
            TimingError = string.Format(CultureInfo.CurrentCulture, Strings.MinutesRange, label, min, max);
            return;
        }

        TimingError = string.Empty;
        var duration = TimeSpan.FromMinutes(minutes);
        _ = ApplyAsync(settings => apply(settings, duration));
    }

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
            IsDocked = settings.Mode == DisturbanceMode.Docked;
            IsRoam = settings.Mode == DisturbanceMode.Roam;
            SedentaryText = Minutes(settings.SedentaryReminderAfter);
            SleepText = Minutes(settings.SleepAfter);
            BossKeyText = settings.BossKey.ToString();
            AutoHideWhenFullscreen = settings.AutoHideWhenFullscreen;
            DetectCoveredMonitor = settings.DetectCoveredMonitor;
            BlacklistText = string.Join(Environment.NewLine, settings.BlacklistedProcesses);
            DevModeEnabled = settings.DevModeEnabled;
            DevProcessesText = string.Join(Environment.NewLine, settings.DevProcesses);
            ScenariosEnabled = settings.ScenariosEnabled;
            MeetingProcessesText = string.Join(Environment.NewLine, settings.MeetingProcesses);
            SelectedLanguage = settings.Language;
            SelectedTheme = settings.Theme;
            SelectedSize = settings.Size;
            ClimbEdges = settings.ClimbEdges;
            ClockOutTimeError = string.Empty;
            ClockOutTimeText = settings.ClockOutTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Formats a duration as whole minutes, rounding up so a short duration never shows as zero.
    /// </summary>
    /// <param name="duration">
    /// The duration.
    /// </param>
    /// <returns>
    /// The number of minutes.
    /// </returns>
    private static string Minutes(TimeSpan duration)
    {
        return ((int)Math.Ceiling(duration.TotalMinutes)).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Shows a boss key that could not be registered when that happens outside an entry by the user.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnBossKeyStatusChanged(object? sender, EventArgs e)
    {
        if (_applyingBossKey)
        {
            return;
        }

        BossKeyError = _bossKey.RegistrationError is { } reason
            ? string.Format(CultureInfo.CurrentCulture, Strings.BossKeyFailed, _settings.Current.BossKey, reason)
            : string.Empty;
    }
}
