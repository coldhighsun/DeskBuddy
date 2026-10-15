namespace DeskBuddy.App.Interop;

/// <summary>
/// Win32 calls about the window the user is working in.
/// </summary>
internal static partial class ForegroundWindowHelper
{
    /// <summary>
    /// Gets the window the user is working in.
    /// </summary>
    /// <returns>
    /// The window handle, or 0 if there is none at the moment.
    /// </returns>
    public static nint Get()
    {
        return GetForegroundWindow();
    }

    /// <summary>
    /// Asks whether a window belongs to this application.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the window was created by this process.
    /// </returns>
    public static bool BelongsToThisProcess(nint window)
    {
        return GetWindowThreadProcessId(window, out var processId) != 0 && processId == Environment.ProcessId;
    }

    /// <summary>
    /// Brings a window of another application back to the front, if it still exists.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the window was brought to the front.
    /// </returns>
    public static bool TryActivate(nint window)
    {
        return IsWindow(window) && SetForegroundWindow(window);
    }

    /// <summary>
    /// Gets the window the user is working in.
    /// </summary>
    /// <returns>
    /// The window handle, or 0 if there is none.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    /// <summary>
    /// Gets the thread that created a window.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <param name="processId">
    /// Receives the id of the process of the window.
    /// </param>
    /// <returns>
    /// The thread id, or 0 if the window does not exist.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    /// <summary>
    /// Asks whether a window handle still stands for a window.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the window exists.
    /// </returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint window);

    /// <summary>
    /// Brings a window to the front and gives it the keyboard focus.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the window was brought to the front.
    /// </returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);
}
