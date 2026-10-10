using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Decides what happens to errors nobody caught. An error on the UI thread is logged and survived once in a while, but a
/// burst of them means the pet is stuck in a failing loop, and then the process is allowed to die instead of spamming.
/// </summary>
public sealed class CrashGuard
{
    /// <summary>
    /// How many UI-thread errors within <see cref="Window"/> are tolerated before giving up.
    /// </summary>
    private const int MaxErrorsInWindow = 5;

    /// <summary>
    /// The span in which UI-thread errors are counted.
    /// </summary>
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The shortest time between two notices to the user.
    /// </summary>
    private static readonly TimeSpan NoticeInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Where the errors are logged.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Tells the user that something went wrong.
    /// </summary>
    private readonly Action _notifyUser;

    /// <summary>
    /// Writes pending log entries to disk, called before the process dies.
    /// </summary>
    private readonly Action _flushLogs;

    /// <summary>
    /// When the recent UI-thread errors happened.
    /// </summary>
    private readonly Queue<DateTimeOffset> _recent = new();

    /// <summary>
    /// When the user was last told, or <see langword="null"/> if never.
    /// </summary>
    private DateTimeOffset? _lastNotice;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrashGuard"/> class.
    /// </summary>
    /// <param name="logger">
    /// Where the errors are logged.
    /// </param>
    /// <param name="time">
    /// The clock.
    /// </param>
    /// <param name="notifyUser">
    /// Tells the user that something went wrong.
    /// </param>
    /// <param name="flushLogs">
    /// Writes pending log entries to disk.
    /// </param>
    public CrashGuard(ILogger logger, TimeProvider time, Action notifyUser, Action flushLogs)
    {
        _logger = logger;
        _time = time;
        _notifyUser = notifyUser;
        _flushLogs = flushLogs;
    }

    /// <summary>
    /// Handles an error that escaped on the UI thread.
    /// </summary>
    /// <param name="exception">
    /// The error.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the application should carry on; <see langword="false"/> if it should be allowed to
    /// terminate because errors keep coming.
    /// </returns>
    public bool HandleUiException(Exception exception)
    {
        var now = _time.GetUtcNow();
        _recent.Enqueue(now);
        while (_recent.Count > 0 && now - _recent.Peek() > Window)
        {
            _recent.Dequeue();
        }

        if (_recent.Count >= MaxErrorsInWindow)
        {
            _logger.LogCritical(exception, "Repeated unhandled errors on the UI thread; the application will exit.");
            _flushLogs();
            return false;
        }

        _logger.LogError(exception, "Unhandled error on the UI thread.");

        if (_lastNotice is not { } last || now - last >= NoticeInterval)
        {
            _lastNotice = now;
            _notifyUser();
        }

        return true;
    }

    /// <summary>
    /// Handles an error that is about to terminate the process.
    /// </summary>
    /// <param name="exception">
    /// The error object; the runtime does not guarantee that it is an <see cref="Exception"/>.
    /// </param>
    public void HandleFatal(object? exception)
    {
        _logger.LogCritical(exception as Exception, "Unhandled error; the process is terminating. Error object: {Error}", exception);
        _flushLogs();
    }

    /// <summary>
    /// Handles the error of a task nobody awaited.
    /// </summary>
    /// <param name="exception">
    /// The error.
    /// </param>
    public void HandleUnobservedTask(Exception exception)
    {
        _logger.LogWarning(exception, "A task failed and nobody observed it.");
    }
}
