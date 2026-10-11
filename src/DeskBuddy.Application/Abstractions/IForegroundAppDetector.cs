namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Tells which application the user is working in.
/// </summary>
public interface IForegroundAppDetector
{
    /// <summary>
    /// Gets the name of the process that owns the foreground window.
    /// </summary>
    /// <returns>
    /// The process name without <c>.exe</c>, or <see langword="null"/> if there is no foreground window or its process
    /// cannot be read (for example a protected system process).
    /// </returns>
    string? GetForegroundProcessName();
}
