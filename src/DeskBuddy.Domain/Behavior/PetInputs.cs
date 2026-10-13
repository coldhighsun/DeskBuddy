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
/// <param name="HasAlert">
/// Whether a message is on screen and the pet should draw attention to it.
/// </param>
/// <param name="Ambient">
/// A state the pet should hold for as long as it is set, because of something going on around it (a build running, a
/// meeting in front), or <see langword="null"/> for none. It ranks above Tired and Coding.
/// </param>
/// <param name="IsDeveloping">
/// Whether the user is working in a development tool (see <see cref="Activity.DevActivityPolicy"/>), so the pet should
/// sit down at its own computer.
/// </param>
public readonly record struct PetInputs(
    TimeSpan UserIdle,
    bool IsSystemStrained = false,
    bool HasAlert = false,
    bool IsDeveloping = false,
    PetState? Ambient = null);
