namespace DeskBuddy.App.Interop;

/// <summary>
/// Turns off window ghosting for this process. When a window stops answering for a few seconds, Windows puts a "ghost" in
/// its place that the user can click, move or close; closing it ends the process. A pet that stalls for a moment because
/// the system is busy would then be killed by the next click on it, so the ghost is not wanted: the pet window has no
/// title bar to close it by anyway, and the tray icon and the task manager remain for leaving a frozen application.
/// </summary>
public static partial class ProcessGhosting
{
    /// <summary>
    /// Turns ghosting off.
    /// </summary>
    /// <param name="logger">Where a failure is logged.</param>
    /// <param name="disable">What turns it off; the call to Windows if omitted. Tests give their own.</param>
    /// <returns><see langword="true"/> if ghosting is off now; <see langword="false"/> if Windows has no such call.</returns>
    public static bool TryDisable(ILogger logger, Action? disable = null)
    {
        try
        {
            (disable ?? DisableProcessWindowsGhosting)();
            return true;
        }
        catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException)
        {
            logger.LogWarning(exception, "Window ghosting could not be turned off.");
            return false;
        }
    }

    /// <summary>
    /// <c>DisableProcessWindowsGhosting</c> of user32.dll.
    /// </summary>
    [LibraryImport("user32.dll")]
    private static partial void DisableProcessWindowsGhosting();
}
