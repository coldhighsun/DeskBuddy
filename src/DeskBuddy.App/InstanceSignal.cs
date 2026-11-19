namespace DeskBuddy.App;

/// <summary>
/// A named event through which another process (a second start of the application, or the installer) asks the running
/// instance to do something, such as close. The running instance listens; others signal.
/// </summary>
public sealed class InstanceSignal : IDisposable
{
    /// <summary>
    /// The named event that is set to signal.
    /// </summary>
    private readonly EventWaitHandle _event;

    /// <summary>
    /// The thread-pool wait that runs the callback each time the event is set.
    /// </summary>
    private readonly RegisteredWaitHandle _registration;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceSignal"/> class and starts listening.
    /// </summary>
    /// <param name="name">
    /// Event name. Use the <c>Local\</c> prefix to scope it to the current session.
    /// </param>
    /// <param name="onSignaled">
    /// Runs on a thread-pool thread each time the event is set; marshal to the UI thread as needed.
    /// </param>
    public InstanceSignal(string name, Action onSignaled)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(onSignaled);

        _event = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, name);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _event,
            (_, _) => onSignaled(),
            state: null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);
    }

    /// <summary>
    /// Signals the instance listening on the given event.
    /// </summary>
    /// <param name="name">
    /// Event name.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if an instance was listening and has been signaled.
    /// </returns>
    public static bool TrySignal(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!EventWaitHandle.TryOpenExisting(name, out var handle))
        {
            return false;
        }

        using (handle)
        {
            return handle.Set();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _registration.Unregister(null);
        _event.Dispose();
    }
}
