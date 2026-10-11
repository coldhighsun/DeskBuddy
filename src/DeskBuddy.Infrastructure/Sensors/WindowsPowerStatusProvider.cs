using System.Runtime.InteropServices;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Power;

namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Reads the power supply with <c>GetSystemPowerStatus</c>.
/// </summary>
public sealed partial class WindowsPowerStatusProvider : IPowerStatusProvider
{
    /// <summary>
    /// Battery flag bit set when the computer has no battery.
    /// </summary>
    private const byte NoBatteryFlag = 128;

    /// <summary>
    /// Value of the battery flag and the charge when the status is not known.
    /// </summary>
    private const byte Unknown = 255;

    /// <summary>
    /// <c>ACLineStatus</c> value for mains power.
    /// </summary>
    private const byte AcOnline = 1;

    /// <inheritdoc />
    public PowerStatus GetStatus()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return default;
        }

        var hasBattery = status.BatteryFlag != Unknown && (status.BatteryFlag & NoBatteryFlag) == 0;
        int? percent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : null;
        return new PowerStatus(hasBattery, percent, status.AcLineStatus == AcOnline);
    }

    /// <summary>
    /// Matches the Win32 <c>SYSTEM_POWER_STATUS</c> structure.
    /// </summary>
    private struct SystemPowerStatus
    {
        /// <summary>
        /// 0 on battery, 1 on mains power, 255 unknown.
        /// </summary>
        public byte AcLineStatus;

        /// <summary>
        /// Charge state flags; 128 means there is no battery, 255 unknown.
        /// </summary>
        public byte BatteryFlag;

        /// <summary>
        /// The charge from 0 to 100, or 255 if unknown.
        /// </summary>
        public byte BatteryLifePercent;

        /// <summary>
        /// Battery saver flag.
        /// </summary>
        public byte SystemStatusFlag;

        /// <summary>
        /// Seconds of battery life left, or <c>-1</c> if unknown.
        /// </summary>
        public int BatteryLifeTime;

        /// <summary>
        /// Seconds of battery life when fully charged, or <c>-1</c> if unknown.
        /// </summary>
        public int BatteryFullLifeTime;
    }

    /// <summary>
    /// Gets the power status of the system.
    /// </summary>
    /// <param name="status">
    /// Receives the status.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);
}
