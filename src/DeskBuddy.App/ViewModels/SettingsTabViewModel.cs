using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.App.Presentation;
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
    /// The settings being edited.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// The installed pets, whose pictures show what each size looks like; <see langword="null"/> if there are none to show.
    /// </summary>
    private readonly InstalledPets? _pets;

    /// <summary>
    /// What <see cref="SizeChoices"/> was made for: the pet in use, the size in use and the version of the pet list.
    /// Only a change in one of them needs new choices, which rebuilds the buttons that show them.
    /// </summary>
    private (string PetName, PetSize Size, int PetsVersion)? _sizeChoicesFor;

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
    /// How to reach the text and the explanation of each process list, as the lists are handled alike.
    /// </summary>
    private readonly Dictionary<ProcessListKind, ProcessListBox> _processLists;

    /// <summary>
    /// What was taken out of the process lists when the settings were loaded.
    /// </summary>
    private readonly IReadOnlyList<RemovedProcessName> _removedAtLoad;

    /// <summary>
    /// The process lists the user has changed since, whose removed names the notice no longer lists.
    /// </summary>
    private readonly HashSet<ProcessListKind> _editedLists = [];

    /// <summary>
    /// How many times each process list was edited, indexed by <see cref="ProcessListKind"/>, to tell whether an edit
    /// is still the newest when its update finishes.
    /// </summary>
    private readonly int[] _processListEdits = new int[Enum.GetValues<ProcessListKind>().Length];

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
    /// Whether the pet now and then says something unprompted.
    /// </summary>
    [ObservableProperty]
    private bool _chatterEnabled;

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
    /// Gets or sets the explanation shown when the full-screen list cannot be used; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _blacklistError = string.Empty;

    /// <summary>
    /// Gets or sets the development tools, one process name per line.
    /// </summary>
    [ObservableProperty]
    private string _devProcessesText = string.Empty;

    /// <summary>
    /// Gets or sets the explanation shown when the list of development tools cannot be used; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _devProcessesError = string.Empty;

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
    /// Gets or sets the explanation shown when the list of meeting applications cannot be used; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _meetingProcessesError = string.Empty;

    /// <summary>
    /// Gets or sets the chat applications, one process name per line.
    /// </summary>
    [ObservableProperty]
    private string _chatProcessesText = string.Empty;

    /// <summary>
    /// Gets or sets the explanation shown when the list of chat applications cannot be used; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _chatProcessesError = string.Empty;

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
    /// Gets or sets the sizes to choose from, each drawn with the pet in use at the size it gets on screen.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SizeChoice> _sizeChoices = [];

    /// <summary>
    /// Gets or sets a value indicating whether the pet may climb the sides of the screen and hang from its top edge.
    /// </summary>
    [ObservableProperty]
    private bool _climbEdges;

    /// <summary>
    /// Gets or sets the hour of the time of day the working day ends.
    /// </summary>
    [ObservableProperty]
    private int _clockOutHour;

    /// <summary>
    /// Gets or sets the minute of the time of day the working day ends.
    /// </summary>
    [ObservableProperty]
    private int _clockOutMinute;

    /// <summary>
    /// Gets or sets the minutes to choose from for the end of the working day: every ten minutes, plus the minute in
    /// use if an older version of the settings left it off that grid.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<int> _minuteChoices = ClockOutMinuteChoices(0);

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
    /// Applies the choice to let the pet say something unprompted now and then.
    /// </summary>
    /// <param name="value">
    /// Whether the pet may chat.
    /// </param>
    partial void OnChatterEnabledChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { ChatterEnabled = value });
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
    /// Applies the edited list of full-screen programs.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnBlacklistTextChanged(string value)
    {
        if (!_refreshing)
        {
            ApplyProcessList(ProcessListKind.Fullscreen, value);
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
        if (!_refreshing)
        {
            ApplyProcessList(ProcessListKind.Meeting, value);
        }
    }

    /// <summary>
    /// Applies the edited list of chat applications.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnChatProcessesTextChanged(string value)
    {
        if (!_refreshing)
        {
            ApplyProcessList(ProcessListKind.Chat, value);
        }
    }

    /// <summary>
    /// Gets or sets what was taken out of the process lists when the settings were loaded, for the user to know; empty
    /// if nothing was. Names drop out of it once the user edits their list or the one they stayed in.
    /// </summary>
    [ObservableProperty]
    private string _processListNotice = string.Empty;

    /// <summary>
    /// Gets the hours to choose from for the end of the working day.
    /// </summary>
    public IReadOnlyList<int> HourChoices { get; } = [.. Enumerable.Range(0, 24)];


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
        UpdateSizeChoices(_settings.Current.PetName);
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { Size = value });
        }
    }

    /// <summary>
    /// Chooses a size, which is applied at once; what the size buttons of the tab do when they are pressed.
    /// </summary>
    /// <param name="size">
    /// The size.
    /// </param>
    [RelayCommand]
    private void ChooseSize(PetSize size)
    {
        SelectedSize = size;
    }

    /// <summary>
    /// Draws the sizes again, for the size chosen and the pet in use, if what they show has changed.
    /// </summary>
    /// <param name="petName">
    /// The folder name of the pet in use.
    /// </param>
    private void UpdateSizeChoices(string petName)
    {
        var key = (petName, SelectedSize, PetsVersion: _pets?.Version ?? 0);
        if (key == _sizeChoicesFor)
        {
            return;
        }

        _sizeChoicesFor = key;
        SizeChoices = PetSizeChoices.For(_pets?.Entries ?? [], petName, SelectedSize);
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
    /// Applies the chosen hour at which the working day ends.
    /// </summary>
    /// <param name="value">
    /// The hour, 0 to 23.
    /// </param>
    partial void OnClockOutHourChanged(int value)
    {
        ApplyClockOutTime();
    }

    /// <summary>
    /// Applies the chosen minute at which the working day ends.
    /// </summary>
    /// <param name="value">
    /// The minute, 0 to 59.
    /// </param>
    partial void OnClockOutMinuteChanged(int value)
    {
        ApplyClockOutTime();
    }

    /// <summary>
    /// Applies the time at which the working day ends from the chosen hour and minute.
    /// </summary>
    private void ApplyClockOutTime()
    {
        if (_refreshing)
        {
            return;
        }

        var time = new TimeSpan(ClockOutHour, ClockOutMinute, 0);
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
        if (!_refreshing)
        {
            ApplyProcessList(ProcessListKind.Development, value);
        }
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
    /// Applies an edited process list, unless a name is in another list; then it is not applied and the reason is shown
    /// until the text is fixed.
    /// </summary>
    /// <param name="kind">
    /// The list.
    /// </param>
    /// <param name="text">
    /// The text entered, one process name per line.
    /// </param>
    private void ApplyProcessList(ProcessListKind kind, string text)
    {
        _ = ApplyProcessListAsync(kind, text);
    }

    /// <summary>
    /// Checks and applies an edited process list. The check is made once on what is shown and once more inside the
    /// update, on the settings as they are when it runs, as an earlier save may still have been under way before. A
    /// newer edit of the same list takes over the explanation, so an update that finishes late does not overwrite it.
    /// </summary>
    /// <param name="kind">
    /// The list.
    /// </param>
    /// <param name="text">
    /// The text entered, one process name per line.
    /// </param>
    /// <returns>
    /// A task that completes when the list has been applied or refused.
    /// </returns>
    private async Task ApplyProcessListAsync(ProcessListKind kind, string text)
    {
        var edit = ++_processListEdits[(int)kind];
        var names = ProcessListRules.Clean(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        var check = ProcessListRules.Check(_settings.Current, kind, names);
        if (check.IsValid)
        {
            var ran = false;
            var refused = check;
            await ApplyAsync(settings =>
            {
                ran = true;
                refused = ProcessListRules.Check(settings, kind, names);
                return refused.IsValid ? ProcessListRules.With(settings, kind, names) : settings;
            });
            if (!ran)
            {
                // The settings failed to load, so nothing was applied and there is nothing to say about the list.
                return;
            }

            check = refused;
        }

        if (edit == _processListEdits[(int)kind])
        {
            _processLists[kind].SetError(check.IsValid ? string.Empty : Describe(check));
        }

        if (check.IsValid)
        {
            // The list in use changed, whether or not this is still the newest edit, so what it held back may fit now.
            _editedLists.Add(kind);
            UpdateProcessListNotice();
            RecheckProcessLists(kind);
        }
    }

    /// <summary>
    /// Checks the lists held back by an error again after another list changed, as the change may have removed the
    /// clash.
    /// </summary>
    /// <param name="changed">
    /// The list that was just applied.
    /// </param>
    private void RecheckProcessLists(ProcessListKind changed)
    {
        foreach (var kind in ProcessListRules.All.Where(kind => kind != changed && _processLists[kind].GetError().Length > 0).ToList())
        {
            ApplyProcessList(kind, _processLists[kind].GetText());
        }
    }

    /// <summary>
    /// Shows what was taken out at load, leaving out the names whose list, or the list they stayed in, the user has
    /// edited since: the note may no longer be true for them.
    /// </summary>
    private void UpdateProcessListNotice()
    {
        ProcessListNotice = DescribeRemoved([.. _removedAtLoad.Where(item => !_editedLists.Contains(item.List) && (item.KeptIn is null || !_editedLists.Contains(item.KeptIn.Value)))]);
    }

    /// <summary>
    /// Tells what was taken out of the process lists at load.
    /// </summary>
    /// <param name="removed">
    /// The names taken out.
    /// </param>
    /// <returns>
    /// A heading and one line for each name; empty if nothing was taken out.
    /// </returns>
    private static string DescribeRemoved(IReadOnlyList<RemovedProcessName> removed)
    {
        if (removed.Count == 0)
        {
            return string.Empty;
        }

        var lines = removed.Select(item => item.Reason == ProcessListProblem.ContainsSpace
            ? string.Format(CultureInfo.CurrentCulture, Strings.ProcessRemovedHasSpace, item.Name, Strings.ProcessListName(item.List))
            : string.Format(CultureInfo.CurrentCulture, Strings.ProcessRemovedInOtherList, item.Name, Strings.ProcessListName(item.List), Strings.ProcessListName(item.KeptIn!.Value)));
        return string.Join(Environment.NewLine, [Strings.ProcessesRemovedNotice, .. lines]);
    }

    /// <summary>
    /// Explains why a process list cannot be used.
    /// </summary>
    /// <param name="check">
    /// The failed check.
    /// </param>
    /// <returns>
    /// The explanation.
    /// </returns>
    private static string Describe(ProcessListCheck check)
    {
        return check.Problem == ProcessListProblem.ContainsSpace
            ? string.Format(CultureInfo.CurrentCulture, Strings.ProcessNameHasSpace, check.Name)
            : string.Format(CultureInfo.CurrentCulture, Strings.ProcessNameInOtherList, check.Name, Strings.ProcessListName(check.OtherList!.Value));
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
    /// Draws the sizes again when the pets that can be chosen changed, as the pet in use may have just become known.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnPetsChanged(object? sender, EventArgs e)
    {
        // Only the pictures of the sizes depend on the pets; the rest of the tab may hold entries not applied yet.
        UpdateSizeChoices(_settings.Current.PetName);
    }

    /// <summary>
    /// Works out the minutes to choose from for the end of the working day.
    /// </summary>
    /// <param name="current">
    /// The minute in use.
    /// </param>
    /// <returns>
    /// Every ten minutes of the hour, with the current minute added if it is not one of them.
    /// </returns>
    private static IReadOnlyList<int> ClockOutMinuteChoices(int current)
    {
        return [.. Enumerable.Range(0, 6).Select(step => step * 10).Append(current).Distinct().Order()];
    }

    /// <summary>
    /// Shows a process list from the settings, unless the text typed for it is held back by an error: that text is
    /// still being fixed and must not be replaced by the list in use.
    /// </summary>
    /// <param name="kind">
    /// The list.
    /// </param>
    /// <param name="settings">
    /// The settings to show.
    /// </param>
    private void ShowProcessList(ProcessListKind kind, PetSettings settings)
    {
        var list = _processLists[kind];
        if (list.GetError().Length == 0)
        {
            list.SetText(string.Join(Environment.NewLine, ProcessListRules.Get(settings, kind)));
        }
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

    /// <summary>
    /// Reaches the text and the explanation of one process list.
    /// </summary>
    /// <param name="GetText">
    /// Reads the text typed.
    /// </param>
    /// <param name="SetText">
    /// Shows a text.
    /// </param>
    /// <param name="GetError">
    /// Reads the explanation shown.
    /// </param>
    /// <param name="SetError">
    /// Shows an explanation.
    /// </param>
    private sealed record ProcessListBox(Func<string> GetText, Action<string> SetText, Func<string> GetError, Action<string> SetError);
}
