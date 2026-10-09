namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Controls whether the application starts when the user signs in. The system's own record is the source of truth, so
/// the answer also reflects changes the user made outside the application (for example in Task Manager).
/// </summary>
public interface IAutoStartService
{
    /// <summary>
    /// Determines whether this installation of the application starts at sign-in.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if it is registered and not disabled by the user; <see langword="false"/> otherwise,
    /// including when the state cannot be read.
    /// </returns>
    bool IsEnabled();

    /// <summary>
    /// Turns starting at sign-in on or off.
    /// </summary>
    /// <param name="enabled">
    /// <see langword="true"/> to start at sign-in.
    /// </param>
    /// <param name="error">
    /// Why the change could not be made (for example a policy forbids it); <see langword="null"/> on success.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the change was made.
    /// </returns>
    bool TrySet(bool enabled, out string? error);
}
