namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Reports how long the user has been away from keyboard and mouse.
/// </summary>
public interface IIdleDetector
{
    /// <summary>
    /// Gets the time since the user's last keyboard or mouse input.
    /// </summary>
    /// <returns>
    /// The idle duration; <see cref="TimeSpan.Zero"/> if it cannot be determined.
    /// </returns>
    TimeSpan GetIdleTime();
}
