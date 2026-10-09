using System.Runtime.InteropServices;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.SystemLoad;

namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Reads CPU utilization with <c>GetSystemTimes</c> and memory usage with <c>GlobalMemoryStatusEx</c>.
/// </summary>
public sealed partial class WindowsSystemLoadMonitor : ISystemLoadMonitor
{
    /// <summary>
    /// Cumulative idle time at the previous reading, in 100-nanosecond units.
    /// </summary>
    private ulong _previousIdle;

    /// <summary>
    /// Cumulative kernel time (which includes idle time) at the previous reading.
    /// </summary>
    private ulong _previousKernel;

    /// <summary>
    /// Cumulative user time at the previous reading.
    /// </summary>
    private ulong _previousUser;

    /// <summary>
    /// Whether a previous reading exists to compute a delta against.
    /// </summary>
    private bool _hasPrevious;

    /// <inheritdoc />
    public SystemLoadSnapshot GetSnapshot()
    {
        var cpu = 0.0;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            if (_hasPrevious)
            {
                cpu = ComputeCpuPercent(_previousIdle, _previousKernel, _previousUser, idle, kernel, user);
            }

            (_previousIdle, _previousKernel, _previousUser, _hasPrevious) = (idle, kernel, user, true);
        }

        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        var memoryPercent = GlobalMemoryStatusEx(ref memory) ? memory.MemoryLoad : 0;

        return new SystemLoadSnapshot(cpu, memoryPercent);
    }

    /// <summary>
    /// Computes CPU utilization between two cumulative <c>GetSystemTimes</c> readings. Kernel time already includes
    /// idle time, so busy time is <c>(kernel + user) - idle</c>.
    /// </summary>
    /// <param name="previousIdle">
    /// Idle time at the earlier reading.
    /// </param>
    /// <param name="previousKernel">
    /// Kernel time at the earlier reading.
    /// </param>
    /// <param name="previousUser">
    /// User time at the earlier reading.
    /// </param>
    /// <param name="idle">
    /// Idle time at the later reading.
    /// </param>
    /// <param name="kernel">
    /// Kernel time at the later reading.
    /// </param>
    /// <param name="user">
    /// User time at the later reading.
    /// </param>
    /// <returns>
    /// Utilization from 0 to 100; 0 if no time elapsed between the readings.
    /// </returns>
    public static double ComputeCpuPercent(
        ulong previousIdle,
        ulong previousKernel,
        ulong previousUser,
        ulong idle,
        ulong kernel,
        ulong user)
    {
        var total = (double)(kernel - previousKernel) + (user - previousUser);
        if (total <= 0)
        {
            return 0;
        }

        var busy = total - (idle - previousIdle);
        return Math.Clamp(busy / total * 100, 0, 100);
    }

    /// <summary>
    /// Retrieves cumulative system CPU times.
    /// </summary>
    /// <param name="idle">
    /// Receives the idle time in 100-nanosecond units.
    /// </param>
    /// <param name="kernel">
    /// Receives the kernel time, including idle time.
    /// </param>
    /// <param name="user">
    /// Receives the user time.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    /// <summary>
    /// Retrieves physical memory usage.
    /// </summary>
    /// <param name="status">
    /// Receives the status; <see cref="MemoryStatus.Length"/> must be set by the caller.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatus status);

    /// <summary>
    /// Matches the Win32 <c>MEMORYSTATUSEX</c> structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        /// <summary>
        /// Size of the structure in bytes.
        /// </summary>
        public uint Length;

        /// <summary>
        /// Approximate percentage of physical memory in use.
        /// </summary>
        public uint MemoryLoad;

        /// <summary>
        /// Total physical memory in bytes.
        /// </summary>
        public ulong TotalPhys;

        /// <summary>
        /// Available physical memory in bytes.
        /// </summary>
        public ulong AvailPhys;

        /// <summary>
        /// Total page file in bytes.
        /// </summary>
        public ulong TotalPageFile;

        /// <summary>
        /// Available page file in bytes.
        /// </summary>
        public ulong AvailPageFile;

        /// <summary>
        /// Total virtual address space in bytes.
        /// </summary>
        public ulong TotalVirtual;

        /// <summary>
        /// Available virtual address space in bytes.
        /// </summary>
        public ulong AvailVirtual;

        /// <summary>
        /// Reserved; always zero.
        /// </summary>
        public ulong AvailExtendedVirtual;
    }
}
