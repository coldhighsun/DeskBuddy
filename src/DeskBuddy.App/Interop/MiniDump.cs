using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Writes a minidump of this process with <c>MiniDumpWriteDump</c>. Meant to be called from a thread other than the one
/// that is stuck: the dump holds the stack of every thread, which is what shows where a frozen UI thread waits.
/// </summary>
public static partial class MiniDump
{
    /// <summary>
    /// <c>MiniDumpWithDataSegs</c>: the data sections of the modules.
    /// </summary>
    private const uint WithDataSegs = 0x00000001;

    /// <summary>
    /// <c>MiniDumpWithIndirectlyReferencedMemory</c>: memory the stacks point into.
    /// </summary>
    private const uint WithIndirectlyReferencedMemory = 0x00000040;

    /// <summary>
    /// <c>MiniDumpWithProcessThreadData</c>: the process and thread environment blocks.
    /// </summary>
    private const uint WithProcessThreadData = 0x00000100;

    /// <summary>
    /// <c>MiniDumpWithUnloadedModules</c>: the list of the modules that were unloaded.
    /// </summary>
    private const uint WithUnloadedModules = 0x00000020;

    /// <summary>
    /// <c>MiniDumpWithThreadInfo</c>: times and states of the threads.
    /// </summary>
    private const uint WithThreadInfo = 0x00001000;

    /// <summary>
    /// What goes into the dump: every thread's stack and what it points to, but not the whole memory of the process, so a
    /// dump stays a size that can be kept and sent.
    /// </summary>
    private const uint DumpType =
        WithDataSegs | WithIndirectlyReferencedMemory | WithProcessThreadData | WithUnloadedModules | WithThreadInfo;

    /// <summary>
    /// Writes a minidump of the current process to a file.
    /// </summary>
    /// <param name="file">The handle of the file to write to; it must be open for writing.</param>
    /// <returns><see langword="true"/> if the dump was written; <see langword="false"/> if Windows refused.</returns>
    public static bool Write(SafeFileHandle file)
    {
        using var self = Process.GetCurrentProcess();
        return MiniDumpWriteDump(self.Handle, (uint)self.Id, file, DumpType, 0, 0, 0);
    }

    /// <summary>
    /// <c>MiniDumpWriteDump</c> of dbghelp.dll.
    /// </summary>
    /// <param name="process">The process handle.</param>
    /// <param name="processId">The process id.</param>
    /// <param name="file">The file to write to.</param>
    /// <param name="dumpType">What to put in the dump.</param>
    /// <param name="exception">Exception information; none.</param>
    /// <param name="userStreams">User streams; none.</param>
    /// <param name="callback">A callback; none.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MiniDumpWriteDump(
        nint process,
        uint processId,
        SafeFileHandle file,
        uint dumpType,
        nint exception,
        nint userStreams,
        nint callback);
}
