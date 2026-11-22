using DeskBuddy.Application;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Detects "do not disturb" situations. The shell's <c>SHQueryUserNotificationState</c> (the signal Windows uses to
/// decide whether to show toast notifications) reports full-screen apps, presentations and Focus Assist. On top of that
/// the user's settings can name processes that count as full screen whenever they are in front (meeting tools that
/// do not report themselves) and turn on a check for any foreground window that covers a whole monitor.
/// </summary>
/// <param name="settings">The settings that hold the process list and the monitor-cover option.</param>
/// <param name="foregroundApp">
/// Tells which process is in front; shared with the other sensors so its cache spares a lookup on every update.
/// </param>
public sealed unsafe partial class WindowsFullscreenDetector(SettingsService settings, IForegroundAppDetector foregroundApp)
    : IFullscreenDetector
{
    /// <summary>
    /// <c>GWL_STYLE</c>: reads the window style.
    /// </summary>
    private const int GwlStyle = -16;

    /// <summary>
    /// <c>WS_CAPTION</c>: the window has a title bar.
    /// </summary>
    private const int WsCaption = 0x00C00000;

    /// <summary>
    /// <c>DWMWA_EXTENDED_FRAME_BOUNDS</c>: the visible bounds of a window, without the invisible resize borders.
    /// </summary>
    private const int DwmwaExtendedFrameBounds = 9;

    /// <summary>
    /// Window classes of the desktop and taskbar, which fill a monitor without being a full-screen application.
    /// </summary>
    private static readonly string[] ShellWindowClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    /// <inheritdoc />
    public DoNotDisturbLevel GetLevel()
    {
        var shellState = SHQueryUserNotificationState(out var state) == 0 ? state : 0;
        var current = settings.Current;

        string? process = null;
        var coversMonitor = false;
        if (current.BlacklistedProcesses.Count > 0 || current.DetectCoveredMonitor)
        {
            var window = ForegroundWindow.GetHandle();
            if (window != 0)
            {
                process = current.BlacklistedProcesses.Count > 0 ? foregroundApp.GetForegroundProcessName() : null;
                coversMonitor = current.DetectCoveredMonitor && CoversMonitor(window);
            }
        }

        return Classify(shellState, process, coversMonitor, current);
    }

    /// <summary>
    /// Decides whether a <c>QUERY_USER_NOTIFICATION_STATE</c> value means notices should be held back.
    /// </summary>
    /// <param name="state">
    /// The state reported by the shell: 1 not present (screen saver or locked), 2 busy (full-screen app or
    /// presentation settings), 3 full-screen Direct3D app, 4 presentation mode, 5 accepts notifications,
    /// 6 quiet time (Focus Assist), 7 windowed Store app.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for states 1, 2, 3, 4 and 6; <see langword="false"/> otherwise, including unknown values.
    /// </returns>
    public static bool IsSuppressing(int state)
    {
        return state is 1 or 2 or 3 or 4 or 6;
    }

    /// <summary>
    /// Combines the shell state with what is known about the foreground window.
    /// </summary>
    /// <param name="shellState">
    /// The <c>QUERY_USER_NOTIFICATION_STATE</c> value; see <see cref="IsSuppressing"/>.
    /// </param>
    /// <param name="foregroundProcess">
    /// Name of the foreground window's process without <c>.exe</c>, or <see langword="null"/> if unknown or not
    /// looked up.
    /// </param>
    /// <param name="foregroundCoversMonitor">
    /// Whether the foreground window covers a whole monitor (only meaningful when the option is on).
    /// </param>
    /// <param name="settings">The current settings.</param>
    /// <returns>
    /// <see cref="DoNotDisturbLevel.Fullscreen"/> for a full-screen or presenting application, a blacklisted process
    /// in front, or (if enabled) a window covering a monitor; <see cref="DoNotDisturbLevel.Quiet"/> for the remaining
    /// suppressing shell states; otherwise <see cref="DoNotDisturbLevel.None"/>.
    /// </returns>
    public static DoNotDisturbLevel Classify(
        int shellState,
        string? foregroundProcess,
        bool foregroundCoversMonitor,
        PetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (shellState is 2 or 3 or 4)
        {
            return DoNotDisturbLevel.Fullscreen;
        }

        if (foregroundProcess is not null
            && settings.BlacklistedProcesses.Contains(foregroundProcess, StringComparer.OrdinalIgnoreCase))
        {
            return DoNotDisturbLevel.Fullscreen;
        }

        if (settings.DetectCoveredMonitor && foregroundCoversMonitor)
        {
            return DoNotDisturbLevel.Fullscreen;
        }

        return IsSuppressing(shellState) ? DoNotDisturbLevel.Quiet : DoNotDisturbLevel.None;
    }

    /// <summary>
    /// Decides whether a window counts as covering its monitor. A window with a title bar that is merely maximized
    /// does not: on a monitor without a taskbar (or with one that hides itself) it fills the screen, but the user is
    /// just working in it.
    /// </summary>
    /// <param name="window">The visible bounds of the window, in device pixels.</param>
    /// <param name="monitor">The bounds of its monitor, in device pixels.</param>
    /// <param name="maximizedWithCaption">Whether the window is maximized and has a title bar.</param>
    /// <returns>
    /// <see langword="true"/> if the window is at least as large as its monitor and is not an ordinary maximized
    /// window.
    /// </returns>
    public static bool IsCoveringMonitor(
        (int Left, int Top, int Right, int Bottom) window,
        (int Left, int Top, int Right, int Bottom) monitor,
        bool maximizedWithCaption)
    {
        if (maximizedWithCaption)
        {
            return false;
        }

        return window.Left <= monitor.Left
            && window.Top <= monitor.Top
            && window.Right >= monitor.Right
            && window.Bottom >= monitor.Bottom;
    }

    /// <summary>
    /// Determines whether a window covers the whole of the monitor it is on. The desktop and the taskbar are not
    /// counted.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <returns>
    /// <see langword="true"/> if the window counts as covering its monitor; see <see cref="IsCoveringMonitor"/>.
    /// </returns>
    private static bool CoversMonitor(nint window)
    {
        const int classNameCapacity = 64;
        var className = stackalloc char[classNameCapacity];
        var classNameLength = GetClassName(window, className, classNameCapacity);
        if (classNameLength > 0 && ShellWindowClasses.Contains(new string(className, 0, classNameLength), StringComparer.Ordinal))
        {
            return false;
        }

        var monitor = MonitorFromWindow(window, 0);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        // The extended frame bounds leave out the invisible resize borders that GetWindowRect includes, which would
        // make a window slightly smaller than its monitor look as if it covered it.
        NativeRect rect;
        if (DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, &rect, sizeof(NativeRect)) != 0
            && !GetWindowRect(window, out rect))
        {
            return false;
        }

        var maximizedWithCaption = IsZoomed(window) && (GetWindowLong(window, GwlStyle) & WsCaption) == WsCaption;
        return IsCoveringMonitor(
            (rect.Left, rect.Top, rect.Right, rect.Bottom),
            (info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom),
            maximizedWithCaption);
    }

    /// <summary>
    /// Rectangle in device pixels, matching the Win32 <c>RECT</c> layout.
    /// </summary>
    private struct NativeRect
    {
        /// <summary>
        /// Left edge.
        /// </summary>
        public int Left;

        /// <summary>
        /// Top edge.
        /// </summary>
        public int Top;

        /// <summary>
        /// Right edge.
        /// </summary>
        public int Right;

        /// <summary>
        /// Bottom edge.
        /// </summary>
        public int Bottom;
    }

    /// <summary>
    /// Matches the Win32 <c>MONITORINFO</c> structure.
    /// </summary>
    private struct MonitorInfo
    {
        /// <summary>
        /// Size of the structure in bytes.
        /// </summary>
        public int Size;

        /// <summary>
        /// Full monitor bounds.
        /// </summary>
        public NativeRect Monitor;

        /// <summary>
        /// Monitor bounds excluding the taskbar and docked toolbars.
        /// </summary>
        public NativeRect Work;

        /// <summary>
        /// Monitor flags.
        /// </summary>
        public uint Flags;
    }

    /// <summary>
    /// Queries the current notification state of the user's session.
    /// </summary>
    /// <param name="state">Receives the <c>QUERY_USER_NOTIFICATION_STATE</c> value.</param>
    /// <returns>An HRESULT; zero on success.</returns>
    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out int state);

    /// <summary>
    /// Reads an attribute of a window from the desktop window manager.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <param name="attribute">The <c>DWMWINDOWATTRIBUTE</c> to read.</param>
    /// <param name="value">Receives the value.</param>
    /// <param name="size">Size of <paramref name="value"/> in bytes.</param>
    /// <returns>An HRESULT; zero on success.</returns>
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint window, int attribute, void* value, int size);

    /// <summary>
    /// Tells whether a window is maximized.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <returns><see langword="true"/> if it is maximized.</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsZoomed(nint window);

    /// <summary>
    /// Reads a 32-bit value of a window, such as its style.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <param name="index">Which value to read, for example <see cref="GwlStyle"/>.</param>
    /// <returns>The value, or zero on failure.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(nint window, int index);

    /// <summary>
    /// Gets the class name of a window.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <param name="className">Receives the name.</param>
    /// <param name="maxCount">Capacity of <paramref name="className"/>.</param>
    /// <returns>The number of characters copied, or zero on failure.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    private static partial int GetClassName(nint window, char* className, int maxCount);

    /// <summary>
    /// Gets the bounds of a window in screen device pixels.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <param name="rect">Receives the bounds.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint window, out NativeRect rect);

    /// <summary>
    /// Gets the monitor that contains or is nearest to a window.
    /// </summary>
    /// <param name="window">The window handle.</param>
    /// <param name="flags">Fallback behavior when the window is on no monitor; zero returns no monitor.</param>
    /// <returns>The monitor handle, or zero.</returns>
    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint window, uint flags);

    /// <summary>
    /// Retrieves information about a monitor.
    /// </summary>
    /// <param name="monitor">Monitor handle.</param>
    /// <param name="info">Receives the information; <see cref="MonitorInfo.Size"/> must be set by the caller.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
