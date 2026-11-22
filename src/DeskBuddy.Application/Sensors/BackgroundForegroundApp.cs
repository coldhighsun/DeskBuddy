namespace DeskBuddy.Application.Sensors;

/// <summary>
/// Answers <see cref="IForegroundAppDetector"/> from the last reading of a <see cref="BackgroundSensor{T}"/>, so opening
/// the foreground process never holds up the thread that asks. The several parts that ask in one tick share one reading.
/// </summary>
/// <param name="sensor">The sensor that reads the real detector.</param>
public sealed class BackgroundForegroundApp(BackgroundSensor<string?> sensor) : IForegroundAppDetector
{
    /// <inheritdoc />
    public string? GetForegroundProcessName()
    {
        return sensor.GetValue();
    }
}
