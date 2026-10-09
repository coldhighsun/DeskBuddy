using System.Runtime.InteropServices;
using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Detects user inactivity with the Win32 <c>GetLastInputInfo</c> API.
/// </summary>
public sealed partial class Win32IdleDetector : IIdleDetector
{
    /// <inheritdoc />
    public TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info) ? ComputeIdle((uint)Environment.TickCount64, info.Time) : TimeSpan.Zero;
    }

    /// <summary>
    /// Computes the idle time from two 32-bit millisecond tick counts, tolerating the counter wrapping around
    /// (roughly every 49.7 days).
    /// </summary>
    /// <param name="nowTicks">
    /// Current tick count in milliseconds.
    /// </param>
    /// <param name="lastInputTicks">
    /// Tick count of the last user input in milliseconds.
    /// </param>
    /// <returns>
    /// The elapsed time between the two tick counts.
    /// </returns>
    public static TimeSpan ComputeIdle(uint nowTicks, uint lastInputTicks)
    {
        return TimeSpan.FromMilliseconds(unchecked(nowTicks - lastInputTicks));
    }

    /// <summary>
    /// Retrieves the time of the last input event.
    /// </summary>
    /// <param name="info">
    /// Receives the last-input information; <see cref="LastInputInfo.Size"/> must be set by the caller.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo info);

    /// <summary>
    /// Matches the Win32 <c>LASTINPUTINFO</c> structure.
    /// </summary>
    private struct LastInputInfo
    {
        /// <summary>
        /// Size of the structure in bytes.
        /// </summary>
        public uint Size;

        /// <summary>
        /// Tick count of the last input event.
        /// </summary>
        public uint Time;
    }
}
