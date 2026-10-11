namespace DeskBuddy.Domain.Power;

/// <summary>
/// How the computer is powered.
/// </summary>
/// <param name="HasBattery">
/// Whether the computer has a battery at all (desktops do not).
/// </param>
/// <param name="BatteryPercent">
/// The charge from 0 to 100, or <see langword="null"/> if it is not known.
/// </param>
/// <param name="IsPluggedIn">
/// Whether the computer is on mains power.
/// </param>
public readonly record struct PowerStatus(bool HasBattery, int? BatteryPercent, bool IsPluggedIn);
