namespace DeskBuddy.Application.Sensors;

/// <summary>
/// Answers <see cref="IProcessList"/> from the last reading of a <see cref="BackgroundSensor{T}"/>, so walking the
/// processes of the machine never holds up the thread that asks.
/// </summary>
/// <param name="sensor">The sensor that reads the real process list.</param>
public sealed class BackgroundProcessList(BackgroundSensor<IReadOnlySet<string>> sensor) : IProcessList
{
    /// <inheritdoc />
    public IReadOnlySet<string> GetNames()
    {
        return sensor.GetValue();
    }
}
