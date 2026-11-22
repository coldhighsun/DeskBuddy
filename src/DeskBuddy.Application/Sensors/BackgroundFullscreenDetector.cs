namespace DeskBuddy.Application.Sensors;

/// <summary>
/// Answers <see cref="IFullscreenDetector"/> from the last reading of a <see cref="BackgroundSensor{T}"/>, so the shell
/// call behind it never holds up the thread that asks. Whatever knows that the situation may have changed, such as
/// another window coming to the front, can have it read at once with <see cref="Refresh"/>, and
/// <see cref="Changed"/> tells when the answer is a different one.
/// </summary>
public sealed class BackgroundFullscreenDetector : IFullscreenDetector
{
    /// <summary>
    /// The sensor that reads the real detector.
    /// </summary>
    private readonly BackgroundSensor<DoNotDisturbLevel> _sensor;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundFullscreenDetector"/> class.
    /// </summary>
    /// <param name="sensor">The sensor that reads the real detector.</param>
    public BackgroundFullscreenDetector(BackgroundSensor<DoNotDisturbLevel> sensor)
    {
        _sensor = sensor;
        _sensor.Changed += (_, e) => Changed?.Invoke(this, e);
    }

    /// <summary>
    /// Raised, from a thread of the thread pool, when a reading shows another level than the one before.
    /// </summary>
    public event EventHandler? Changed;

    /// <inheritdoc />
    public DoNotDisturbLevel GetLevel()
    {
        return _sensor.GetValue();
    }

    /// <summary>
    /// Reads the level now, because something says it may have changed. Never waits for the system.
    /// </summary>
    public void Refresh()
    {
        _sensor.Refresh();
    }
}
