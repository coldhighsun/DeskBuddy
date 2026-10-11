using System.Runtime.InteropServices;

namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Win32 lookups for the window the user is working in, shared by the sensors that care about it.
/// </summary>
internal static unsafe partial class ForegroundWindow
{
    /// <summary>
    /// <c>PROCESS_QUERY_LIMITED_INFORMATION</c>: enough to read a process's image name, and allowed even for
    /// elevated processes.
    /// </summary>
    private const uint ProcessQueryLimitedInformation = 0x1000;

    /// <summary>
    /// Gets the window the user is working in.
    /// </summary>
    /// <returns>
    /// The window handle, or zero if there is none.
    /// </returns>
    [LibraryImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    public static partial nint GetHandle();

    /// <summary>
    /// Gets the name of the process that owns a window, without the <c>.exe</c> extension.
    /// </summary>
    /// <param name="window">
    /// The window handle.
    /// </param>
    /// <returns>
    /// The process name, or <see langword="null"/> if it cannot be read (for example for protected system processes).
    /// </returns>
    public static string? GetProcessName(nint window)
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
