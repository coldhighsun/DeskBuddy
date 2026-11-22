namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Keeps the books of the UI-thread watchdog: a probe is posted to the UI thread, and if it is not answered within the
/// threshold, the thread is stalled. It knows nothing about threads or dispatchers, so the rules can be tested with a
/// clock; <see cref="UiStallMonitor"/> does the posting. The watchdog thread and the UI thread both call it.
/// </summary>
/// <param name="threshold">How long a probe may go unanswered before the UI thread counts as stalled.</param>
public sealed class StallTracker(TimeSpan threshold)
{
    /// <summary>
    /// Guards the fields, which two threads touch.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// When the probe that has not been answered yet was posted, or <see langword="null"/> if none is outstanding.
    /// </summary>
    private DateTimeOffset? _probeSentAt;

    /// <summary>
    /// Whether the stall of the outstanding probe has been reported already.
    /// </summary>
    private bool _reported;

    /// <summary>
    /// Registers that a probe is about to be posted.
    /// </summary>
    /// <param name="now">The time.</param>
    /// <returns>
    /// <see langword="true"/> if the probe is to be posted; <see langword="false"/> if the previous one is still
    /// unanswered, in which case no second one piles up behind it.
    /// </returns>
    public bool TryBeginProbe(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_probeSentAt is not null)
            {
                return false;
            }

            _probeSentAt = now;
            return true;
        }
    }

    /// <summary>
    /// Asks whether the outstanding probe has gone unanswered for too long.
    /// </summary>
    /// <param name="now">The time.</param>
    /// <returns>
    /// How long the probe has been waiting, the first time that is longer than the threshold; <see langword="null"/>
    /// otherwise, including for a stall that was reported already, so one stall is reported once.
    /// </returns>
    public TimeSpan? CheckStalled(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_probeSentAt is not { } sentAt || _reported || now - sentAt < threshold)
            {
                return null;
            }

            _reported = true;
            return now - sentAt;
        }
    }

    /// <summary>
    /// Registers that the UI thread answered the outstanding probe.
    /// </summary>
    /// <param name="now">The time.</param>
    /// <returns>
    /// How long the probe was waiting if a stall was reported for it, so that the end of that stall can be reported;
    /// <see langword="null"/> if the thread answered in time.
    /// </returns>
    public TimeSpan? Answer(DateTimeOffset now)
    {
        lock (_gate)
        {
            var waited = _probeSentAt is { } sentAt ? now - sentAt : TimeSpan.Zero;
            var wasReported = _reported;
            _probeSentAt = null;
            _reported = false;
            return wasReported ? waited : null;
        }
    }
}
