namespace DeskBuddy.Domain.Pomodoro;

/// <summary>
/// What kind of phase boundary a <see cref="PomodoroSession"/> crossed during an update.
/// </summary>
public enum PomodoroTransitionKind
{
    /// <summary>
    /// Nothing happened.
    /// </summary>
    None,

    /// <summary>
    /// A work phase ran to its end.
    /// </summary>
    WorkCompleted,

    /// <summary>
    /// A break ran to its end; the session now waits for the user to start the next round.
    /// </summary>
    BreakCompleted,
}

/// <summary>
/// Result of <see cref="PomodoroSession.Update"/>.
/// </summary>
/// <param name="Kind">
/// Which boundary was crossed, if any.
/// </param>
/// <param name="IsStale">
/// <see langword="true"/> if the phase had expired long ago (see <see cref="PomodoroOptions.StaleAfter"/>), in which
/// case the session was stopped silently and no reminder should be shown.
/// </param>
public readonly record struct PomodoroTransition(PomodoroTransitionKind Kind, bool IsStale = false)
{
    /// <summary>
    /// Gets a transition meaning nothing happened.
    /// </summary>
    public static PomodoroTransition NoChange => new(PomodoroTransitionKind.None);
}
