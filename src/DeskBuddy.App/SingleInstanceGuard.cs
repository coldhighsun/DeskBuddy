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
    /// Initializes a new instance of the <see cref="SingleInstanceGuard"/> class and tries to take ownership.
    /// </summary>
    /// <param name="name">
    /// Mutex name. Use the <c>Local\</c> prefix to scope it to the current session.
    /// </param>
    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirstInstance = createdNew;
    }

    /// <summary>
    /// Gets a value indicating whether this process is the first (and only) running instance.
    /// </summary>
    public bool IsFirstInstance { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
