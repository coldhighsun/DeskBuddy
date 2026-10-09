namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Detects situations where the user must not be disturbed by pop-ups: a full-screen app, a presentation, Focus
/// Assist, or a locked screen.
/// </summary>
public interface IFullscreenDetector
{
    /// <summary>
    /// Determines whether notices should be held back right now.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the user should not be disturbed; <see langword="false"/> if it is fine to show a
    /// notice, or if the state cannot be determined.
    /// </returns>
    bool IsDoNotDisturb();
}
