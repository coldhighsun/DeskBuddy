using Microsoft.Win32;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Reads a change of the Windows session as the session being locked or unlocked.
/// </summary>
internal static class SessionLock
{
    /// <summary>
    /// Tells what a session change means for the pet.
    /// </summary>
    /// <param name="reason">
    /// Why the session changed.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the session was locked, <see langword="false"/> when it was unlocked, or
    /// <see langword="null"/> for any other change, which leaves the pet as it is.
    /// </returns>
    public static bool? IsLocked(SessionSwitchReason reason)
    {
        return reason switch
        {
            SessionSwitchReason.SessionLock => true,
            SessionSwitchReason.SessionUnlock => false,
            _ => null,
        };
    }
}
