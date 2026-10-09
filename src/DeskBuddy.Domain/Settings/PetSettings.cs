namespace DeskBuddy.Domain.Settings;

/// <summary>
/// The user's preferences. Instances are immutable; <see cref="Normalize"/> brings out-of-range values back into a
/// range the rest of the application can work with.
/// </summary>
public sealed record PetSettings
{
    /// <summary>
    /// Skin used when none is configured.
    /// </summary>
    public const string DefaultSkinName = "default";

    /// <summary>
    /// Suffix stripped from process names so <c>zoom</c> and <c>Zoom.exe</c> mean the same thing.
    /// </summary>
    private const string ExecutableSuffix = ".exe";

    /// <summary>
    /// Shortest allowed length of a pomodoro phase or break.
    /// </summary>
    public static readonly TimeSpan MinPhaseDuration = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Longest allowed length of a pomodoro work phase.
    /// </summary>
    public static readonly TimeSpan MaxWorkDuration = TimeSpan.FromHours(3);

    /// <summary>
    /// Longest allowed length of a pomodoro break.
    /// </summary>
    public static readonly TimeSpan MaxBreakDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Shortest allowed time of inactivity before the pet falls asleep.
    /// </summary>
    public static readonly TimeSpan MinSleepAfter = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Longest allowed time of inactivity before the pet falls asleep.
    /// </summary>
    public static readonly TimeSpan MaxSleepAfter = TimeSpan.FromHours(12);

    /// <summary>
    /// Shortest allowed interval between sedentary reminders.
    /// </summary>
    public static readonly TimeSpan MinSedentaryInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Longest allowed interval between sedentary reminders.
    /// </summary>
    public static readonly TimeSpan MaxSedentaryInterval = TimeSpan.FromHours(8);

    /// <summary>
    /// Gets the settings used before the user changes anything.
    /// </summary>
    public static PetSettings Default { get; } = new();

    /// <summary>
    /// Gets how much room the pet takes up on the desktop.
    /// </summary>
    public DisturbanceMode Mode { get; init; } = DisturbanceMode.Docked;

    /// <summary>
    /// Gets the name of the skin folder under <c>skins</c>.
    /// </summary>
    public string SkinName { get; init; } = DefaultSkinName;

    /// <summary>
    /// Gets how long the user must be inactive before the pet falls asleep.
    /// </summary>
    public TimeSpan SleepAfter { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the length of a pomodoro work phase.
    /// </summary>
    public TimeSpan PomodoroWork { get; init; } = TimeSpan.FromMinutes(25);

    /// <summary>
    /// Gets the length of a short pomodoro break.
    /// </summary>
    public TimeSpan PomodoroShortBreak { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the length of a long pomodoro break.
    /// </summary>
    public TimeSpan PomodoroLongBreak { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets how long the user can work without a break before the sedentary reminder.
    /// </summary>
    public TimeSpan SedentaryReminderAfter { get; init; } = TimeSpan.FromMinutes(45);

    /// <summary>
    /// Gets the global shortcut that hides and shows the pet.
    /// </summary>
    public HotkeyGesture BossKey { get; init; } = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'H');

    /// <summary>
    /// Gets a value indicating whether the pet hides itself while a full-screen or presenting application is in front.
    /// </summary>
    public bool AutoHideWhenFullscreen { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether a foreground window covering a whole monitor also counts as full screen. This
    /// catches borderless games and some meeting tools the system does not report, at the price of the occasional false
    /// alarm, so it is off by default.
    /// </summary>
    public bool DetectCoveredMonitor { get; init; }

    /// <summary>
    /// Gets the names of processes (without <c>.exe</c>) that count as full screen whenever they are in front.
    /// </summary>
    public IReadOnlyList<string> BlacklistedProcesses { get; init; } = [];

    /// <summary>
    /// Returns a copy with every value forced into its allowed range: durations are clamped, unknown modes, blank
    /// skin names and invalid shortcuts fall back to the defaults, and the process list is trimmed, stripped of
    /// <c>.exe</c> and de-duplicated ignoring case.
    /// </summary>
    /// <returns>
    /// The normalized settings.
    /// </returns>
    public PetSettings Normalize()
    {
        var defaults = Default;
        return this with
        {
            Mode = Enum.IsDefined(Mode) ? Mode : defaults.Mode,
            SkinName = string.IsNullOrWhiteSpace(SkinName) ? DefaultSkinName : SkinName.Trim(),
            SleepAfter = Clamp(SleepAfter, MinSleepAfter, MaxSleepAfter),
            PomodoroWork = Clamp(PomodoroWork, MinPhaseDuration, MaxWorkDuration),
            PomodoroShortBreak = Clamp(PomodoroShortBreak, MinPhaseDuration, MaxBreakDuration),
            PomodoroLongBreak = Clamp(PomodoroLongBreak, MinPhaseDuration, MaxBreakDuration),
            SedentaryReminderAfter = Clamp(SedentaryReminderAfter, MinSedentaryInterval, MaxSedentaryInterval),
            BossKey = BossKey.IsValid ? BossKey : defaults.BossKey,
            BlacklistedProcesses = NormalizeProcessNames(BlacklistedProcesses),
        };
    }

    /// <summary>
    /// Limits a duration to a range.
    /// </summary>
    /// <param name="value">
    /// The duration.
    /// </param>
    /// <param name="min">
    /// The lower bound.
    /// </param>
    /// <param name="max">
    /// The upper bound.
    /// </param>
    /// <returns>
    /// The duration, moved to the nearest bound if it was outside.
    /// </returns>
    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        return value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Cleans up a list of process names.
    /// </summary>
    /// <param name="names">
    /// The names as entered, possibly <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The distinct, non-blank names without surrounding spaces or <c>.exe</c>.
    /// </returns>
    private static string[] NormalizeProcessNames(IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            return [];
        }

        return
        [
            .. names
                .Select(name => name?.Trim() ?? string.Empty)
                .Select(name => name.EndsWith(ExecutableSuffix, StringComparison.OrdinalIgnoreCase)
                    ? name[..^ExecutableSuffix.Length].TrimEnd()
                    : name)
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }
}
