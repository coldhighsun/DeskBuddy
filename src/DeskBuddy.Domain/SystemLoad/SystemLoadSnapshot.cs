namespace DeskBuddy.Domain.SystemLoad;

/// <summary>
/// A point-in-time reading of how busy the machine is.
/// </summary>
/// <param name="CpuPercent">
/// Total CPU utilization since the previous reading, from 0 to 100.
/// </param>
/// <param name="MemoryPercent">
/// Physical memory in use, from 0 to 100.
/// </param>
public readonly record struct SystemLoadSnapshot(double CpuPercent, double MemoryPercent);
