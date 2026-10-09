namespace DeskBuddy.Domain.Pomodoro;

/// <summary>
/// The phase a <see cref="PomodoroSession"/> is in.
/// </summary>
public enum PomodoroPhase
{
    /// <summary>
    /// No pomodoro is running; waiting for the user to start one.
    /// </summary>
    Stopped,

    /// <summary>
    /// A focused work phase.
    /// </summary>
    Work,

    /// <summary>
    /// A short break after a work phase.
    /// </summary>
    ShortBreak,

    /// <summary>
    /// A long break after several work phases.
    /// </summary>
    LongBreak,
}
