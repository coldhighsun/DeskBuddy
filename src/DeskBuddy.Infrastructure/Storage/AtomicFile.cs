using System.Text;

namespace DeskBuddy.Infrastructure.Storage;

/// <summary>
/// Reads and writes files so that a crash or power loss never leaves a half-written file behind, and so that the brief
/// locks antivirus and indexing tools put on freshly written files do not turn into failures.
/// </summary>
internal static class AtomicFile
{
    /// <summary>
    /// How many times an operation is tried when the file is in use by another process.
    /// </summary>
    private const int MaxAttempts = 5;

    /// <summary>
    /// Win32 error code for a sharing violation (the file is open in another process).
    /// </summary>
    private const int ErrorSharingViolation = 32;

    /// <summary>
    /// Win32 error code for a lock violation (part of the file is locked by another process).
    /// </summary>
    private const int ErrorLockViolation = 33;

    /// <summary>
    /// UTF-8 without a byte order mark.
    /// </summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Pause between attempts.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Replaces a file's content by writing a temporary file next to it and then moving it over the original.
    /// </summary>
    /// <param name="path">
    /// The file to write. Its directory is created if missing.
    /// </param>
    /// <param name="content">
    /// The complete new content.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the file has been replaced.
    /// </returns>
    public static Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        return RetryWhileInUseAsync(
            async () =>
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temporaryPath = path + ".tmp";
                try
                {
                    await File.WriteAllTextAsync(temporaryPath, content, Utf8NoBom, cancellationToken);
                    File.Move(temporaryPath, path, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }

                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Reads a whole UTF-8 file.
    /// </summary>
    /// <param name="path">
    /// The file to read.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The file content.
    /// </returns>
    public static Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        return RetryWhileInUseAsync(
            () => File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Runs a file operation, trying again a few times if another process briefly has the file open. Any other
    /// failure, and a lock that outlasts the attempts, is thrown to the caller.
    /// </summary>
    /// <typeparam name="T">
    /// The result type of the operation.
    /// </typeparam>
    /// <param name="operation">
    /// The operation to run.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The result of the operation.
    /// </returns>
    private static async Task<T> RetryWhileInUseAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsInUse(ex))
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Determines whether an exception means the file is open in another process. Windows reports that as a sharing
    /// or lock violation when opening the file, but as "access denied" when replacing or deleting it.
    /// </summary>
    /// <param name="exception">
    /// The exception to inspect.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if retrying shortly may succeed.
    /// </returns>
    private static bool IsInUse(Exception exception)
    {
        return exception is UnauthorizedAccessException
            || (exception is IOException io && (io.HResult & 0xFFFF) is ErrorSharingViolation or ErrorLockViolation);
    }
}
