namespace DeskBuddy.App;

/// <summary>
/// Ensures only one instance of the application runs per user session, using a named mutex.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>
    /// The named mutex held for the lifetime of the guard.
    /// </summary>
    private readonly Mutex _mutex;

    /// <summary>
    /// Whether this process owns the mutex and must release it.
    /// </summary>
    private bool _owned;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleInstanceGuard"/> class and tries to take ownership.
    /// </summary>
    /// <param name="name">
    /// Mutex name. Use the <c>Local\</c> prefix to scope it to the current session.
    /// </param>
    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirstInstance = createdNew;
        _owned = createdNew;
    }

    /// <summary>
    /// Gets a value indicating whether this process is the first (and only) running instance.
    /// </summary>
    public bool IsFirstInstance { get; }

    /// <summary>
    /// Waits for the first instance to exit, for a later instance that asked it to close.
    /// </summary>
    /// <param name="timeout">
    /// How long to wait at most.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the first instance has exited (or there was none) within the time.
    /// </returns>
    public bool WaitForFirstInstanceToExit(TimeSpan timeout)
    {
        if (_owned)
        {
            return true;
        }

        try
        {
            _owned = _mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // The first instance ended without releasing the mutex; it is gone all the same, and the mutex is ours.
            _owned = true;
        }

        return _owned;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_owned)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
