using DeskBuddy.Domain.Power;

namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Tells how the computer is powered.
/// </summary>
public interface IPowerStatusProvider
{
    /// <summary>
    /// Gets the current power status.
    /// </summary>
    /// <returns>
    /// Whether there is a battery, how full it is and whether the computer is plugged in.
    /// </returns>
    PowerStatus GetStatus();
}
