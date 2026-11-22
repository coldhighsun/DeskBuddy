using ThrottledLogging;

namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Logs the sensor readings that take long enough to be felt, with the name of the sensor. A reading made for the pet's
/// brain on the UI thread that shows up here is a stall; a reading made in the background only delays the next answer,
/// and is logged so a system call that waits for another program can be found. A reading that is slow every time is
/// logged once in a while, not at every tick: the log is throttled per sensor, and the entry that resumes after
/// suppression says how many were left out. Readings are reported from the UI thread and from the thread pool.
/// </summary>
/// <param name="logger">Where the slow readings and failures are logged.</param>
/// <param name="minGap">The shortest time between two entries about the same sensor; <see cref="SlowTickLog.MinGap"/> if omitted.</param>
public sealed class SlowSensorLog(ILogger logger, TimeSpan? minGap = null)
{
    /// <summary>
    /// How long a reading may take before it is logged.
    /// </summary>
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets the diagnostics a <see cref="BackgroundSensor{T}"/> reports to.
    /// </summary>
    public SensorDiagnostics Diagnostics => new(Report, Failed);

    /// <summary>
    /// Reports how long a reading took, and logs it if it was slow.
    /// </summary>
    /// <param name="sensor">Which sensor was read.</param>
    /// <param name="elapsed">How long the reading took.</param>
    public void Report(string sensor, TimeSpan elapsed)
    {
        if (elapsed < Threshold)
        {
            return;
        }

        logger.LogWarningThrottled(
            $"slow-sensor:{sensor}",
            minGap ?? SlowTickLog.MinGap,
            "Reading the {Sensor} took {ElapsedMs:F0} ms.",
            sensor,
            elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Logs a reading that threw.
    /// </summary>
    /// <param name="sensor">Which sensor was read.</param>
    /// <param name="exception">What the reading threw.</param>
    public void Failed(string sensor, Exception exception)
    {
        logger.LogWarning(exception, "Reading the {Sensor} failed; its last reading stays in use.", sensor);
    }
}
