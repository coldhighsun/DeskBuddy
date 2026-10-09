namespace DeskBuddy.Domain.SystemLoad;

/// <summary>
/// Thresholds that decide when the pet reacts to system load. The gap between the "high" and "recover" values forms
/// a hysteresis band that prevents flickering between states.
/// </summary>
public sealed class SystemLoadOptions
{
    /// <summary>
    /// Gets or sets the CPU utilization, in percent, at or above which the system counts as strained.
    /// </summary>
    public double HighCpuPercent { get; set; } = 85;

    /// <summary>
    /// Gets or sets the CPU utilization, in percent, at or below which a strained system counts as recovered.
    /// </summary>
    public double RecoverCpuPercent { get; set; } = 60;

    /// <summary>
    /// Gets or sets the memory usage, in percent, at or above which the system counts as strained.
    /// </summary>
    public double HighMemoryPercent { get; set; } = 90;

    /// <summary>
    /// Gets or sets the memory usage, in percent, at or below which a strained system counts as recovered.
    /// </summary>
    public double RecoverMemoryPercent { get; set; } = 80;

    /// <summary>
    /// Gets or sets how long load must stay high before the system counts as strained, so short spikes are ignored.
    /// </summary>
    public TimeSpan HighDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how long load must stay low before a strained system counts as recovered.
    /// </summary>
    public TimeSpan RecoverDuration { get; set; } = TimeSpan.FromSeconds(10);
}
