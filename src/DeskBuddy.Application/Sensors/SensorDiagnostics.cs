namespace DeskBuddy.Application.Sensors;

/// <summary>
/// Where a <see cref="BackgroundSensor{T}"/> reports how long a reading took and that one failed. Both may be called from
/// a thread of the thread pool.
/// </summary>
/// <param name="Timed">Called with the name of the sensor and how long a reading took.</param>
/// <param name="Failed">Called with the name of the sensor and the exception a reading threw.</param>
public sealed record SensorDiagnostics(Action<string, TimeSpan> Timed, Action<string, Exception> Failed);
