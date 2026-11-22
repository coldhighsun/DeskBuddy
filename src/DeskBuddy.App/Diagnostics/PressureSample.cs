using System.Diagnostics;

namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// What the machine and this process were up to at one moment: enough to tell, when the UI thread stalls, whether the
/// process was starved of CPU (the machine was busy), blocked (it got hardly any CPU time and paged memory in) or neither.
/// Two samples taken around a stall are turned into a line for the log by <see cref="Describe"/>.
/// </summary>
/// <param name="At">When the sample was taken.</param>
/// <param name="ProcessCpu">CPU time this process has used so far.</param>
/// <param name="WorkingSetBytes">Physical memory this process holds.</param>
/// <param name="PageFaults">Page faults this process has had so far; a page brought back from disk or from the compression store counts.</param>
/// <param name="SystemIdleTicks">Idle time of all processors so far, in 100-nanosecond units.</param>
/// <param name="SystemTotalTicks">Kernel plus user time of all processors so far (kernel time includes idle time).</param>
/// <param name="AvailablePhysicalBytes">Physical memory not in use.</param>
/// <param name="TotalPhysicalBytes">Physical memory installed.</param>
/// <param name="CommitPercent">How much of the commit limit (RAM plus page file) is promised, from 0 to 100.</param>
/// <param name="Priority">The priority class of this process.</param>
public sealed partial record PressureSample(
    DateTimeOffset At,
    TimeSpan ProcessCpu,
    long WorkingSetBytes,
    uint PageFaults,
    ulong SystemIdleTicks,
    ulong SystemTotalTicks,
    ulong AvailablePhysicalBytes,
    ulong TotalPhysicalBytes,
    uint CommitPercent,
    ProcessPriorityClass Priority)
{
    /// <summary>
    /// Number of bytes in a megabyte, for the log.
    /// </summary>
    private const double Megabyte = 1024 * 1024;

    /// <summary>
    /// Number of bytes in a gigabyte, for the log.
    /// </summary>
    private const double Gigabyte = 1024 * 1024 * 1024;

    /// <summary>
    /// Reads the process and the machine now. Cheap enough to be done every few tenths of a second; safe to call from any
    /// thread, and it never throws: whatever cannot be read is zero.
    /// </summary>
    /// <param name="process">This process, kept by the caller so that it is not looked up again every time.</param>
    /// <param name="at">The time to record.</param>
    /// <returns>The sample.</returns>
    public static PressureSample Take(Process process, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(process);

        var cpu = TimeSpan.Zero;
        var priority = ProcessPriorityClass.Normal;
        try
        {
            cpu = process.TotalProcessorTime;
            priority = process.PriorityClass;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // The sample is a diagnostic aid; a value that cannot be read stays at its default.
        }

        var counters = new ProcessMemoryCounters { Size = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
        _ = GetProcessMemoryInfo(GetCurrentProcess(), ref counters, counters.Size);

        _ = GetSystemTimes(out var idle, out var kernel, out var user);

        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        _ = GlobalMemoryStatusEx(ref memory);
        var commitPercent = memory.TotalPageFile == 0
            ? 0u
            : (uint)Math.Round((memory.TotalPageFile - memory.AvailPageFile) * 100.0 / memory.TotalPageFile);

        return new PressureSample(
            at,
            cpu,
            (long)counters.WorkingSetSize,
            counters.PageFaultCount,
            idle,
            kernel + user,
            memory.AvailPhys,
            memory.TotalPhys,
            commitPercent,
            priority);
    }

    /// <summary>
    /// Turns two samples into a line for the log.
    /// </summary>
    /// <param name="before">The sample from the start of the period.</param>
    /// <param name="after">The sample from its end.</param>
    /// <returns>
    /// What this process and the machine did in between: the CPU time the process got out of the wall-clock time that
    /// passed (little of it means it was starved or blocked), how busy the whole machine was, the page faults, and how
    /// full the memory is.
    /// </returns>
    public static string Describe(PressureSample before, PressureSample after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var wall = (after.At - before.At).TotalMilliseconds;
        var processCpu = (after.ProcessCpu - before.ProcessCpu).TotalMilliseconds;
        var totalTicks = (double)(after.SystemTotalTicks - before.SystemTotalTicks);
        var machineBusy = totalTicks <= 0
            ? 0
            : Math.Clamp((totalTicks - (after.SystemIdleTicks - before.SystemIdleTicks)) / totalTicks * 100, 0, 100);
        var faults = (long)after.PageFaults - before.PageFaults;
        var workingSet = after.WorkingSetBytes / Megabyte;
        var workingSetChange = (after.WorkingSetBytes - before.WorkingSetBytes) / Megabyte;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"process CPU {processCpu:F0} ms in {wall:F0} ms, machine CPU {machineBusy:F0} %, page faults +{faults}, " +
            $"working set {workingSet:F0} MB ({workingSetChange:+0;-0;0} MB), " +
            $"free RAM {after.AvailablePhysicalBytes / Gigabyte:F1} of {after.TotalPhysicalBytes / Gigabyte:F1} GB, " +
            $"commit {after.CommitPercent} %, priority {after.Priority}");
    }

    /// <summary>
    /// Matches the Win32 <c>PROCESS_MEMORY_COUNTERS</c> structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCounters
    {
        /// <summary>
        /// Size of the structure in bytes.
        /// </summary>
        public uint Size;

        /// <summary>
        /// Page faults so far.
        /// </summary>
        public uint PageFaultCount;

        /// <summary>
        /// Peak working set.
        /// </summary>
        public nuint PeakWorkingSetSize;

        /// <summary>
        /// Current working set.
        /// </summary>
        public nuint WorkingSetSize;

        /// <summary>
        /// Peak paged pool use.
        /// </summary>
        public nuint QuotaPeakPagedPoolUsage;

        /// <summary>
        /// Paged pool use.
        /// </summary>
        public nuint QuotaPagedPoolUsage;

        /// <summary>
        /// Peak non-paged pool use.
        /// </summary>
        public nuint QuotaPeakNonPagedPoolUsage;

        /// <summary>
        /// Non-paged pool use.
        /// </summary>
        public nuint QuotaNonPagedPoolUsage;

        /// <summary>
        /// Commit charge of the process.
        /// </summary>
        public nuint PagefileUsage;

        /// <summary>
        /// Peak commit charge of the process.
        /// </summary>
        public nuint PeakPagefileUsage;
    }

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
        /// Commit limit in bytes.
        /// </summary>
        public ulong TotalPageFile;

        /// <summary>
        /// Commit still available in bytes.
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

    /// <summary>
    /// Gets the pseudo handle of the calling process.
    /// </summary>
    /// <returns>The pseudo handle.</returns>
    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    /// <summary>
    /// Reads the memory counters of a process.
    /// </summary>
    /// <param name="process">The process handle.</param>
    /// <param name="counters">Receives the counters.</param>
    /// <param name="size">Size of <paramref name="counters"/> in bytes.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("psapi.dll", EntryPoint = "GetProcessMemoryInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessMemoryInfo(nint process, ref ProcessMemoryCounters counters, uint size);

    /// <summary>
    /// Retrieves the cumulative CPU times of the machine.
    /// </summary>
    /// <param name="idle">Receives the idle time.</param>
    /// <param name="kernel">Receives the kernel time, which includes idle time.</param>
    /// <param name="user">Receives the user time.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    /// <summary>
    /// Retrieves physical memory and commit usage.
    /// </summary>
    /// <param name="status">Receives the status; its length must be set by the caller.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
