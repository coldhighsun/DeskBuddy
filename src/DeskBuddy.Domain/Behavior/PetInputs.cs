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
public readonly record struct PetInputs(TimeSpan UserIdle, bool IsSystemStrained = false);
