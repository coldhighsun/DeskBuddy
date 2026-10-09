namespace DeskBuddy.Domain.Pomodoro;

/// <summary>
/// Durations that shape a pomodoro cycle.
/// </summary>
public sealed class PomodoroOptions
{
    /// <summary>
    /// Gets or sets the length of a work (focus) phase.
    /// </summary>
    public TimeSpan WorkDuration { get; set; } = TimeSpan.FromMinutes(25);

    /// <summary>
    /// Gets or sets the length of a short break.
    /// </summary>
    public TimeSpan ShortBreakDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the length of a long break.
    /// </summary>
    public TimeSpan LongBreakDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets how many work phases are completed before a long break replaces the short break.
    /// </summary>
    public int LongBreakEvery { get; set; } = 4;

    /// <summary>
    /// Gets or sets how long after a phase should have ended it still counts as a fresh completion. A phase that
    /// expired further back than this (typically because the machine slept or hibernated) ends silently instead of
    /// producing an out-of-date reminder.
    /// </summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(5);
}
