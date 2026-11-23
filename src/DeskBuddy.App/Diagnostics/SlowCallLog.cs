using ThrottledLogging;

namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Logs a call into Windows that took long, by name: <c>SetWindowPos</c> and its kind wait for the window manager when the
/// system is busy, and a freeze then shows in the log as the call that held the thread up. It is what
/// <see cref="Win32Timing.Timed"/> hands the times to. A call that is slow again and again is logged once in a while, per
/// call, with the same throttle as <see cref="SlowTickLog"/>.
/// </summary>
/// <param name="logger">Where the slow calls are logged.</param>
/// <param name="minGap">The shortest time between two entries about the same call; <see cref="SlowTickLog.MinGap"/> if omitted.</param>
public sealed class SlowCallLog(ILogger logger, TimeSpan? minGap = null)
{
    /// <summary>
    /// How long a call may take before it is logged.
    /// </summary>
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Logs the call if it took long.
    /// </summary>
    /// <param name="call">The name of the call.</param>
    /// <param name="elapsed">How long it took.</param>
    public void Report(string call, TimeSpan elapsed)
    {
        if (elapsed < Threshold)
        {
            return;
        }

        logger.LogWarningThrottled(
            $"slow-call:{call}",
            minGap ?? SlowTickLog.MinGap,
            "The call to {Call} took {ElapsedMs:F0} ms.",
            call,
            elapsed.TotalMilliseconds);
    }
}
