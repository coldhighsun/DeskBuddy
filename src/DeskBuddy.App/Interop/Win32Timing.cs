using System.Diagnostics;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Times the calls into Windows that a UI thread makes while the pet moves, so that a call that waits for the window
/// manager (which it does when the system is busy) shows up in the log by name instead of only as a slow tick. What is
/// done with a time is up to <see cref="Timed"/>; nothing is measured but a timestamp when it is not set.
/// </summary>
public static class Win32Timing
{
    /// <summary>
    /// Gets or sets what is told how long a call took (which call, how long); it decides what is slow enough to log.
    /// </summary>
    public static Action<string, TimeSpan>? Timed { get; set; }

    /// <summary>
    /// Notes the time before a call.
    /// </summary>
    /// <returns>The timestamp to give to <see cref="Stop"/>.</returns>
    public static long Start()
    {
        return Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Tells <see cref="Timed"/> how long a call took.
    /// </summary>
    /// <param name="call">The name of the call.</param>
    /// <param name="startedAt">What <see cref="Start"/> returned before the call.</param>
    public static void Stop(string call, long startedAt)
    {
        Timed?.Invoke(call, Stopwatch.GetElapsedTime(startedAt));
    }
}
