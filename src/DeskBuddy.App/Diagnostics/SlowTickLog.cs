namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Logs the timer ticks of the pet window that take long enough to be felt, which says what the UI thread was busy with
/// when <see cref="UiStallMonitor"/> reports that it stalled. A tick that is slow every time is logged once in a while, not
/// thirty times a second.
/// </summary>
/// <param name="logger">Where the slow ticks are logged.</param>
/// <param name="time">The clock.</param>
public sealed class SlowTickLog(ILogger logger, TimeProvider time)
{
    /// <summary>
    /// How long a tick may take before it is logged; the move timer runs every 33 ms, so a tick past this one has cost the
    /// animation a frame or more.
    /// </summary>
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(40);

    /// <summary>
    /// The shortest time between two entries about the same kind of tick.
    /// </summary>
    private static readonly TimeSpan MinGap = TimeSpan.FromSeconds(2);

    /// <summary>
    /// When each kind of tick was last logged.
    /// </summary>
    private readonly Dictionary<string, DateTimeOffset> _lastLogged = [];

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

        var now = time.GetUtcNow();
        if (_lastLogged.TryGetValue(tick, out var last) && now - last < MinGap)
        {
            return;
        }

        _lastLogged[tick] = now;
        logger.LogWarning("The {Tick} tick of the pet window took {ElapsedMs:F0} ms.", tick, elapsed.TotalMilliseconds);
    }
}
