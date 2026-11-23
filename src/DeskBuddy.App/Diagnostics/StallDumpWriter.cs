using System.IO;
using Microsoft.Win32.SafeHandles;

namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Writes a dump of the process when the UI thread is stuck for long, so that a freeze that ends with the user killing
/// the process leaves something to look at: the stack of the UI thread says what it waits for. Only the newest few dumps
/// are kept. A failure to write is logged, never thrown: the watchdog must not make a bad situation worse.
/// </summary>
/// <param name="directory">The folder the dumps go to; created if missing.</param>
/// <param name="keep">How many dumps to keep, the newest ones.</param>
/// <param name="time">The clock, which names the dumps.</param>
/// <param name="logger">Where problems are logged.</param>
/// <param name="write">
/// Writes the dump to a file handle and says whether it worked; the real minidump if omitted. Tests give their own so
/// that no dump of the test process is taken.
/// </param>
/// <param name="minGap">
/// How soon after a dump another one may be taken; <see cref="DefaultMinGap"/> if omitted. A request that comes sooner is
/// given the dump that exists.
/// </param>
public sealed class StallDumpWriter(
    string directory,
    int keep,
    TimeProvider time,
    ILogger logger,
    Func<SafeFileHandle, bool>? write = null,
    TimeSpan? minGap = null)
{
    /// <summary>
    /// How soon after a dump another one may be taken, unless told otherwise. A dump holds the stacks of every thread, so
    /// the second of two stalls that begin together (the application's thread and the pet's) has nothing to add to it.
    /// </summary>
    public static readonly TimeSpan DefaultMinGap = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Lets one dump be written at a time. The two watchdogs share the writer and call it from threads of their own, and
    /// the dump functions of Windows are single-threaded: each suspends the other threads of the process, a second dumper
    /// among them.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// When the last dump was written, or <see langword="null"/> if none has been.
    /// </summary>
    private DateTimeOffset? _lastWrittenAt;

    /// <summary>
    /// The path of the last dump, or <see langword="null"/> if none has been written.
    /// </summary>
    private string? _lastPath;
    /// <summary>
    /// The start of the name of every dump, which is also how old dumps are found.
    /// </summary>
    private const string NamePrefix = "deskbuddy-stall-";

    /// <summary>
    /// The extension of a dump.
    /// </summary>
    private const string Extension = ".dmp";

    /// <summary>
    /// Writes a dump, then removes the oldest ones beyond the number to keep. Safe to call from several threads: they take
    /// turns, and one that comes soon after a dump was written is given that dump instead of a second one.
    /// </summary>
    /// <returns>The path of the dump, or <see langword="null"/> if none could be written.</returns>
    public string? TryWrite()
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            if (_lastPath is not null && _lastWrittenAt is { } last && now - last < (minGap ?? DefaultMinGap))
            {
                return _lastPath;
            }

            string path;
            try
            {
                Directory.CreateDirectory(directory);
                var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                path = Path.Combine(directory, $"{NamePrefix}{stamp}{Extension}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not prepare the folder for the stall dump: {Directory}", directory);
                return null;
            }

            if (!WriteFile(path))
            {
                return null;
            }

            _lastWrittenAt = now;
            _lastPath = path;
            Prune();
            return path;
        }
    }

    /// <summary>
    /// Writes the dump file; a file that did not come out whole is removed.
    /// </summary>
    /// <param name="path">Where to write.</param>
    /// <returns><see langword="true"/> if the file was written.</returns>
    private bool WriteFile(string path)
    {
        FileStream stream;
        try
        {
            // CreateNew: a file that is there already is somebody's dump, and is never touched.
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not create the stall dump {Path}.", path);
            return false;
        }

        // From here on the file is ours, so a dump that does not come out whole is removed.
        var written = false;
        try
        {
            using (stream)
            {
                written = (write ?? new Func<SafeFileHandle, bool>(MiniDump.Write))(stream.SafeFileHandle);
            }

            if (!written)
            {
                logger.LogWarning("Windows could not write the stall dump {Path}.", path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not write the stall dump {Path}.", path);
        }

        if (!written)
        {
            Delete(path);
        }

        return written;
    }

    /// <summary>
    /// Removes the oldest dumps, so that no more than the number to keep are left. The names hold the time, so the
    /// names in order are the dumps in order.
    /// </summary>
    private void Prune()
    {
        try
        {
            var old = Directory.EnumerateFiles(directory, $"{NamePrefix}*{Extension}")
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .Skip(Math.Max(keep, 1));
            foreach (var file in old)
            {
                Delete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not remove old stall dumps from {Directory}.", directory);
        }
    }

    /// <summary>
    /// Deletes a file, logging instead of failing.
    /// </summary>
    /// <param name="path">The file.</param>
    private void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not delete the stall dump {Path}.", path);
        }
    }
}
