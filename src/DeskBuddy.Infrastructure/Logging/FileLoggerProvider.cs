using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.Infrastructure.Logging;

/// <summary>
/// Writes log entries to a file per day. Entries are queued and written by a background task, so logging never blocks the
/// caller, and a failure to write is dropped rather than raised: logging must not be able to crash the pet.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// How many entries may wait to be written; beyond that new entries are dropped.
    /// </summary>
    private const int QueueCapacity = 2000;

    /// <summary>
    /// How long disposing waits for the queue to drain.
    /// </summary>
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The directory the log files are written to.
    /// </summary>
    private readonly string _directory;

    /// <summary>
    /// The clock used for time stamps and for choosing the day's file.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Entries below this level are not written.
    /// </summary>
    private readonly LogLevel _minimumLevel;

    /// <summary>
    /// Entries waiting for the writer task.
    /// </summary>
    private readonly Channel<Entry> _queue = Channel.CreateBounded<Entry>(
        new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    /// <summary>
    /// The background task that drains <see cref="_queue"/>.
    /// </summary>
    private readonly Task _writer;

    /// <summary>
    /// Creates the provider and starts its writer; old log files are deleted first.
    /// </summary>
    /// <param name="directory">
    /// The directory to write to; it is created when missing.
    /// </param>
    /// <param name="time">
    /// The clock.
    /// </param>
    /// <param name="minimumLevel">
    /// The lowest level that is written.
    /// </param>
    /// <param name="keepDays">
    /// How many days of logs are kept.
    /// </param>
    public FileLoggerProvider(string directory, TimeProvider time, LogLevel minimumLevel, int keepDays)
    {
        _directory = directory;
        _time = time;
        _minimumLevel = minimumLevel;

        try
        {
            Directory.CreateDirectory(directory);
            LogRetention.Prune(directory, DateOnly.FromDateTime(time.GetLocalNow().DateTime), keepDays);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Without a directory the writer fails on every entry and drops it; the application still runs.
        }

        _writer = Task.Run(WriteLoopAsync);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _writer.Wait(DrainTimeout);
    }

    /// <summary>
    /// Waits until everything logged so far has been written. Used before the process is about to die, when the
    /// background writer would otherwise lose the last entries.
    /// </summary>
    /// <param name="timeout">
    /// The longest to wait.
    /// </param>
    public void Flush(TimeSpan timeout)
    {
        var marker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_queue.Writer.TryWrite(new Entry(_time.GetLocalNow(), LogLevel.None, string.Empty, string.Empty, null, marker)))
        {
            marker.Task.Wait(timeout);
        }
    }

    /// <summary>
    /// Queues an entry; it is dropped when the queue is full or already closed.
    /// </summary>
    /// <param name="level">
    /// The level of the entry.
    /// </param>
    /// <param name="category">
    /// The logger category.
    /// </param>
    /// <param name="message">
    /// The formatted message.
    /// </param>
    /// <param name="exception">
    /// The exception to append, if any.
    /// </param>
    private void Enqueue(LogLevel level, string category, string message, Exception? exception)
    {
        _queue.Writer.TryWrite(new Entry(_time.GetLocalNow(), level, category, message, exception, null));
    }

    /// <summary>
    /// Writes queued entries until the queue is completed, flushing once each time it runs empty.
    /// </summary>
    /// <returns>
    /// A task that completes when the queue has been completed and drained.
    /// </returns>
    private async Task WriteLoopAsync()
    {
        var reader = _queue.Reader;
        while (await reader.WaitToReadAsync())
        {
            var text = new StringBuilder();
            var markers = new List<TaskCompletionSource>();
            DateOnly? day = null;
            while (reader.TryRead(out var entry))
            {
                if (entry.Flushed is not null)
                {
                    markers.Add(entry.Flushed);
                    continue;
                }

                var entryDay = DateOnly.FromDateTime(entry.Time.DateTime);
                if (day is not null && day != entryDay)
                {
                    Append(day.Value, text);
                    text.Clear();
                }

                day = entryDay;
                Format(text, entry);
            }

            if (day is not null)
            {
                Append(day.Value, text);
            }

            foreach (var marker in markers)
            {
                marker.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Appends text to the log of a day. A failure is ignored, so one locked file only loses its own entries.
    /// </summary>
    /// <param name="day">
    /// The day whose file receives the text.
    /// </param>
    /// <param name="text">
    /// The text to append.
    /// </param>
    private void Append(DateOnly day, StringBuilder text)
    {
        try
        {
            var path = Path.Combine(_directory, LogRetention.FileNameFor(day));
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Dropped on purpose: logging must never take the application down.
        }
    }

    /// <summary>
    /// Formats an entry as one line, followed by its exception when there is one.
    /// </summary>
    /// <param name="text">
    /// The builder to append to.
    /// </param>
    /// <param name="entry">
    /// The entry to format.
    /// </param>
    private static void Format(StringBuilder text, Entry entry)
    {
        text.Append(entry.Time.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
            .Append(" [").Append(Abbreviate(entry.Level)).Append("] ")
            .Append(entry.Category).Append(": ")
            .AppendLine(entry.Message);

        if (entry.Exception is not null)
        {
            text.AppendLine(entry.Exception.ToString());
        }
    }

    /// <summary>
    /// Gets the three-letter tag of a level.
    /// </summary>
    /// <param name="level">
    /// The level.
    /// </param>
    /// <returns>
    /// The tag.
    /// </returns>
    private static string Abbreviate(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "Trc",
            LogLevel.Debug => "Dbg",
            LogLevel.Information => "Inf",
            LogLevel.Warning => "Wrn",
            LogLevel.Error => "Err",
            LogLevel.Critical => "Crt",
            _ => "???",
        };
    }

    /// <summary>
    /// One queued log entry.
    /// </summary>
    /// <param name="Time">
    /// When the entry was logged.
    /// </param>
    /// <param name="Level">
    /// The level.
    /// </param>
    /// <param name="Category">
    /// The logger category.
    /// </param>
    /// <param name="Message">
    /// The formatted message.
    /// </param>
    /// <param name="Exception">
    /// The attached exception, if any.
    /// </param>
    /// <param name="Flushed">
    /// For a flush marker, the source completed once everything before it is written; such an entry has no text.
    /// </param>
    private sealed record Entry(
        DateTimeOffset Time,
        LogLevel Level,
        string Category,
        string Message,
        Exception? Exception,
        TaskCompletionSource? Flushed);

    /// <summary>
    /// A logger that hands its entries to the provider.
    /// </summary>
    /// <param name="category">
    /// The logger category.
    /// </param>
    /// <param name="provider">
    /// The owning provider.
    /// </param>
    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None && logLevel >= provider._minimumLevel;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            provider.Enqueue(logLevel, category, formatter(state, exception), exception);
        }
    }
}
