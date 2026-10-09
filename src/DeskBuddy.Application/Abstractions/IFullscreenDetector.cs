namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Detects situations where the user must not be disturbed: a full-screen app, a presentation, a meeting tool, Focus
/// Assist, or a locked screen.
/// </summary>
public interface IFullscreenDetector
{
    /// <summary>
    /// Determines how much the pet should stay out of the way right now.
    /// </summary>
    /// <returns>
    /// The level; <see cref="DoNotDisturbLevel.None"/> if it is fine to show the pet and notices, or if the situation
    /// cannot be determined.
    /// </returns>
    DoNotDisturbLevel GetLevel();
}
