using System.Runtime.InteropServices;
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
/// <param name="settings">
/// The settings that hold the process list and the monitor-cover option.
/// </param>
public sealed unsafe partial class WindowsFullscreenDetector(SettingsService settings) : IFullscreenDetector
{
    /// <summary>
    /// <c>PROCESS_QUERY_LIMITED_INFORMATION</c>: enough to read a process's image name, and allowed even for
    /// elevated processes.
    /// </summary>
    private const uint ProcessQueryLimitedInformation = 0x1000;

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
            var window = GetForegroundWindow();
            if (window != 0)
            {
                process = current.BlacklistedProcesses.Count > 0 ? GetProcessName(window) : null;
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
    /// <param name="settings">
    /// The current settings.
    /// </param>
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
    /// Gets the name of the process that owns a window, without the <c>.exe</c> extension.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <returns>
    /// The process name, or <see langword="null"/> if it cannot be read (for example for protected system processes).
    /// </returns>
    private static string? GetProcessName(nint window)
    {
        if (GetWindowThreadProcessId(window, out var processId) == 0)
        {
            return null;
        }

        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
        {
            return null;
        }

        try
        {
            var buffer = stackalloc char[1024];
            var length = 1024;
            return QueryFullProcessImageName(process, 0, buffer, ref length)
                ? Path.GetFileNameWithoutExtension(new string(buffer, 0, length))
                : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>
    /// Determines whether a window covers the whole of the monitor it is on. The desktop and the taskbar are not
    /// counted.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the window is at least as large as its monitor.
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
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info) || !GetWindowRect(window, out var rect))
        {
            return false;
        }

        return rect.Left <= info.Monitor.Left
            && rect.Top <= info.Monitor.Top
            && rect.Right >= info.Monitor.Right
            && rect.Bottom >= info.Monitor.Bottom;
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
    /// <param name="state">
    /// Receives the <c>QUERY_USER_NOTIFICATION_STATE</c> value.
    /// </param>
    /// <returns>
    /// An HRESULT; zero on success.
    /// </returns>
    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out int state);

    /// <summary>
    /// Gets the window the user is working in.
    /// </summary>
    /// <returns>
    /// The window handle, or zero if there is none.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    /// <summary>
    /// Gets the thread and process that created a window.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <param name="processId">
    /// Receives the process identifier.
    /// </param>
    /// <returns>
    /// The thread identifier, or zero on failure.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    /// <summary>
    /// Gets the class name of a window.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <param name="className">
    /// Receives the name.
    /// </param>
    /// <param name="maxCount">
    /// Capacity of <paramref name="className"/>.
    /// </param>
    /// <returns>
    /// The number of characters copied, or zero on failure.
    /// </returns>
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    private static partial int GetClassName(nint window, char* className, int maxCount);

    /// <summary>
    /// Gets the bounds of a window in screen device pixels.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <param name="rect">
    /// Receives the bounds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint window, out NativeRect rect);

    /// <summary>
    /// Gets the monitor that contains or is nearest to a window.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <param name="flags">
    /// Fallback behavior when the window is on no monitor; zero returns no monitor.
    /// </param>
    /// <returns>
    /// The monitor handle, or zero.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint window, uint flags);

    /// <summary>
    /// Retrieves information about a monitor.
    /// </summary>
    /// <param name="monitor">
    /// Monitor handle.
    /// </param>
    /// <param name="info">
    /// Receives the information; <see cref="MonitorInfo.Size"/> must be set by the caller.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    /// <summary>
    /// Opens a process.
    /// </summary>
    /// <param name="access">
    /// The access rights requested.
    /// </param>
    /// <param name="inheritHandle">
    /// Whether child processes inherit the handle.
    /// </param>
    /// <param name="processId">
    /// The process identifier.
    /// </param>
    /// <returns>
    /// The process handle, or zero on failure.
    /// </returns>
    [LibraryImport("kernel32.dll")]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    /// <summary>
    /// Gets the full path of a process's executable.
    /// </summary>
    /// <param name="process">
    /// The process handle.
    /// </param>
    /// <param name="flags">
    /// Zero for a Win32 path.
    /// </param>
    /// <param name="exeName">
    /// Receives the path.
    /// </param>
    /// <param name="size">
    /// On input the capacity of <paramref name="exeName"/>, on output the length copied.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(nint process, uint flags, char* exeName, ref int size);

    /// <summary>
    /// Closes a handle.
    /// </summary>
    /// <param name="handle">
    /// The handle to close.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
