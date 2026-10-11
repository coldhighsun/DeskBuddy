using System.Runtime.InteropServices;
using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Lists the running processes with a Toolhelp snapshot, which is much cheaper than opening every process: it yields
/// names only, with no handles to the processes and nothing about their command lines.
/// </summary>
public sealed unsafe partial class WindowsProcessList : IProcessList
{
    /// <summary>
    /// <c>TH32CS_SNAPPROCESS</c>: include all processes in the snapshot.
    /// </summary>
    private const uint SnapProcess = 0x2;

    /// <summary>
    /// The value <c>CreateToolhelp32Snapshot</c> returns on failure.
    /// </summary>
    private static readonly nint InvalidHandle = -1;

    /// <inheritdoc />
    public IReadOnlySet<string> GetNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == InvalidHandle)
        {
            return names;
        }

        try
        {
            var entry = new ProcessEntry { Size = (uint)sizeof(ProcessEntry) };
            for (var found = Process32First(snapshot, ref entry); found; found = Process32Next(snapshot, ref entry))
            {
                names.Add(Path.GetFileNameWithoutExtension(new string(entry.ExeFile)));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return names;
    }

    /// <summary>
    /// Matches the Win32 <c>PROCESSENTRY32W</c> structure.
    /// </summary>
    private struct ProcessEntry
    {
        /// <summary>
        /// Size of the structure in bytes; must be set by the caller.
        /// </summary>
        public uint Size;

        /// <summary>
        /// No longer used.
        /// </summary>
        public uint Usage;

        /// <summary>
        /// The process identifier.
        /// </summary>
        public uint ProcessId;

        /// <summary>
        /// No longer used.
        /// </summary>
        public nuint DefaultHeapId;

        /// <summary>
        /// No longer used.
        /// </summary>
        public uint ModuleId;

        /// <summary>
        /// The number of threads.
        /// </summary>
        public uint Threads;

        /// <summary>
        /// The identifier of the parent process.
        /// </summary>
        public uint ParentProcessId;

        /// <summary>
        /// The base priority of threads the process creates.
        /// </summary>
        public int PriorityClassBase;

        /// <summary>
        /// No longer used.
        /// </summary>
        public uint Flags;

        /// <summary>
        /// The name of the executable file, null-terminated.
        /// </summary>
        public fixed char ExeFile[260];
    }

    /// <summary>
    /// Takes a snapshot of the system's processes.
    /// </summary>
    /// <param name="flags">
    /// What to include; <see cref="SnapProcess"/>.
    /// </param>
    /// <param name="processId">
    /// Ignored for a snapshot of processes.
    /// </param>
    /// <returns>
    /// The snapshot handle, or an invalid handle on failure.
    /// </returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

    /// <summary>
    /// Reads the first process of a snapshot.
    /// </summary>
    /// <param name="snapshot">
    /// The snapshot handle.
    /// </param>
    /// <param name="entry">
    /// Receives the process; its size must be set.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if there was a process.
    /// </returns>
    [LibraryImport("kernel32.dll", EntryPoint = "Process32FirstW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32First(nint snapshot, ref ProcessEntry entry);

    /// <summary>
    /// Reads the next process of a snapshot.
    /// </summary>
    /// <param name="snapshot">
    /// The snapshot handle.
    /// </param>
    /// <param name="entry">
    /// Receives the process.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if there was another process.
    /// </returns>
    [LibraryImport("kernel32.dll", EntryPoint = "Process32NextW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32Next(nint snapshot, ref ProcessEntry entry);

    /// <summary>
    /// Closes a handle.
    /// </summary>
    /// <param name="handle">
    /// The handle to close.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
