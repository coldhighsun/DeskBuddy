namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// How strongly the user's current situation asks the pet to stay out of the way.
/// </summary>
public enum DoNotDisturbLevel
{
    /// <summary>
    /// Nothing special; the pet and its messages are fine.
    /// </summary>
    None,

    /// <summary>
    /// The user wants no notices (Focus Assist, locked screen) but nothing covers the screen, so the pet may stay
    /// while its messages are held back.
    /// </summary>
    Quiet,

    /// <summary>
    /// A full-screen, presenting or blacklisted application is in front. Messages are held back and the pet hides.
    /// </summary>
    Fullscreen,
}
