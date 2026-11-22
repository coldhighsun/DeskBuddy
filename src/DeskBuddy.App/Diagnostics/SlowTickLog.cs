using ThrottledLogging;

namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Logs the timer ticks of the pet window that take long enough to be felt, which says what the UI thread was busy with
/// when <see cref="UiStallMonitor"/> reports that it stalled. A tick that is slow every time is logged once in a while, not
/// thirty times a second: the log is throttled per kind of tick, and the entry that resumes after suppression says how
/// many were left out.
/// </summary>
/// <param name="logger">Where the slow ticks are logged.</param>
/// <param name="minGap">The shortest time between two entries about the same kind of tick; <see cref="MinGap"/> if omitted.</param>
public sealed class SlowTickLog(ILogger logger, TimeSpan? minGap = null)
{
    /// <summary>
    /// How long a tick may take before it is logged; the move timer runs every 33 ms, so a tick past this one has cost the
    /// animation several frames. Set well above one frame so that ordinary hiccups do not fill the log.
    /// </summary>
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The shortest time between two entries about the same kind of slow event, here and in <see cref="SlowSensorLog"/>.
    /// </summary>
    public static readonly TimeSpan MinGap = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Reports how long a tick took, and logs it if it was slow.
    /// </summary>
    /// <param name="tick">Which timer ticked.</param>
    /// <param name="elapsed">How long handling the tick took.</param>
    public void Report(string tick, TimeSpan elapsed)
    {
        if (elapsed < Threshold)
        {
            return;
        }

        logger.LogWarningThrottled(
            $"slow-tick:{tick}",
            minGap ?? MinGap,
            "The {Tick} tick of the pet window took {ElapsedMs:F0} ms.",
            tick,
            elapsed.TotalMilliseconds);
    }
}
