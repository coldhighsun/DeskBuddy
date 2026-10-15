using Serilog;
using Serilog.Events;

namespace DeskBuddy.Infrastructure.Logging;

/// <summary>
/// Builds the Serilog logger that writes DeskBuddy's rolling log files.
/// </summary>
public static class LoggerSetup
{
    /// <summary>
    /// The largest size of one log file; a bigger log continues in a new file.
    /// </summary>
    public const long DefaultFileSizeLimitBytes = 10L * 1024 * 1024;

    /// <summary>
    /// How many log files are kept; the oldest ones beyond this are deleted.
    /// </summary>
    public const int DefaultRetainedFileCount = 5;

    /// <summary>
    /// The layout of one log line.
    /// </summary>
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Creates a logger that writes <c>deskbuddy-yyyyMMdd.log</c> files, starting a new file each day and whenever the
    /// current one reaches the size limit. Writing happens on a background thread so callers never block on disk I/O;
    /// disposing the logger drains the queue. A failure to write is swallowed by Serilog, so logging cannot crash the pet.
    /// </summary>
    /// <param name="directory">
    /// The directory to write to; it is created when missing.
    /// </param>
    /// <param name="minimumLevel">
    /// The lowest level that is written.
    /// </param>
    /// <param name="fileSizeLimitBytes">
    /// The largest size of one file.
    /// </param>
    /// <param name="retainedFileCount">
    /// How many files are kept.
    /// </param>
    /// <returns>
    /// The logger; dispose it to flush and close the file.
    /// </returns>
    public static Serilog.Core.Logger Create(
        string directory,
        LogLevel minimumLevel,
        long fileSizeLimitBytes = DefaultFileSizeLimitBytes,
        int retainedFileCount = DefaultRetainedFileCount)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(ToSerilogLevel(minimumLevel))
            .WriteTo.Async(sink => sink.File(
                Path.Combine(directory, "deskbuddy-.log"),
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: fileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: retainedFileCount,
                shared: true))
            .CreateLogger();
    }

    /// <summary>
    /// Maps a Microsoft logging level to the Serilog one.
    /// </summary>
    /// <param name="level">
    /// The Microsoft level.
    /// </param>
    /// <returns>
    /// The Serilog level.
    /// </returns>
    private static LogEventLevel ToSerilogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Fatal,
        };
    }
}
