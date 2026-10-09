namespace DeskBuddy.Domain.Behavior;

/// <summary>
/// Everything the behavior state machine needs to know about the outside world at one moment.
/// </summary>
/// <param name="UserIdle">
/// How long the user has had no keyboard or mouse input.
/// </param>
/// <param name="IsSystemStrained">
/// Whether the system is under sustained heavy load (see <see cref="SystemLoad.LoadReactionPolicy"/>).
/// </param>
/// <param name="IsFocusing">
/// Whether a pomodoro work phase is running, so the pet should keep quietly still.
/// </param>
/// <param name="HasAlert">
/// Whether a message is on screen and the pet should draw attention to it.
/// </param>
public readonly record struct PetInputs(
    TimeSpan UserIdle,
    bool IsSystemStrained = false,
    bool IsFocusing = false,
    bool HasAlert = false);
