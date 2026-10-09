using DeskBuddy.Domain.SystemLoad;

namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Reports how busy the machine is.
/// </summary>
public interface ISystemLoadMonitor
{
    /// <summary>
    /// Takes a reading of the current system load. CPU utilization covers the interval since the previous call.
    /// </summary>
    /// <returns>
    /// The current load; all zeros if it cannot be determined.
    /// </returns>
    SystemLoadSnapshot GetSnapshot();
}
