using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.Application;

/// <summary>
/// Holds the sticky note text and saves it shortly after the user stops typing, so typing never waits on disk. A
/// failed background save is reported through <see cref="SaveFailed"/> and retried on the next change or flush. If
/// the saved note could not be read at startup, edits are refused so it is never overwritten.
/// </summary>
public sealed class NoteService : IDisposable
{
    /// <summary>
    /// Where the note is saved.
    /// </summary>
    private readonly INoteStore _store;

    /// <summary>
    /// Fires once the user has stopped typing for the save delay.
    /// </summary>
    private readonly ITimer _timer;

    /// <summary>
    /// How long to wait after the last edit before saving.
    /// </summary>
    private readonly TimeSpan _saveDelay;

    /// <summary>
    /// Makes sure only one save runs at a time.
    /// </summary>
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>
    /// Guards <see cref="_text"/> and <see cref="_isDirty"/>, which the UI thread and the save timer both touch.
    /// </summary>
    private readonly object _stateLock = new();

    /// <summary>
    /// The current note text.
    /// </summary>
    private string _text = string.Empty;

    /// <summary>
    /// Whether the text has changes that are not saved yet.
    /// </summary>
    private bool _isDirty;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteService"/> class.
    /// </summary>
    /// <param name="store">
    /// Where the note is saved.
    /// </param>
    /// <param name="timeProvider">
    /// The time source used for the save delay.
    /// </param>
    /// <param name="saveDelay">
    /// How long to wait after the last edit before saving.
    /// </param>
    public NoteService(INoteStore store, TimeProvider timeProvider, TimeSpan saveDelay)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _saveDelay = saveDelay;
        _timer = timeProvider.CreateTimer(OnTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Raised, possibly on a background thread, when a delayed save fails.
    /// </summary>
    public event EventHandler<Exception>? SaveFailed;

    /// <summary>
    /// Gets why loading failed at startup, or <see langword="null"/> if it succeeded. While set, the note is empty and
    /// edits are refused.
    /// </summary>
    public Exception? LoadFailure { get; private set; }

    /// <summary>
    /// Gets the current note text.
    /// </summary>
    public string Text
    {
        get
        {
            lock (_stateLock)
            {
                return _text;
            }
        }
    }

    /// <summary>
    /// Loads the saved note. A storage problem is recorded in <see cref="LoadFailure"/> instead of thrown, so the
    /// application can still start.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when loading has finished.
    /// </returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var text = await _store.LoadAsync(cancellationToken);
            lock (_stateLock)
            {
                _text = text;
                _isDirty = false;
            }

            LoadFailure = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LoadFailure = ex;
        }
    }

    /// <summary>
    /// Replaces the note text and schedules a save after the user stops typing.
    /// </summary>
    /// <param name="text">
    /// The new text.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when loading failed at startup, so edits are refused.
    /// </exception>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (LoadFailure is not null)
        {
            throw new InvalidOperationException(
                "The saved note could not be read, so edits are refused to avoid overwriting it.",
                LoadFailure);
        }

        lock (_stateLock)
        {
            if (text == _text)
            {
                return;
            }

            _text = text;
            _isDirty = true;
        }

        _timer.Change(_saveDelay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Saves right away if there are unsaved changes. Call when the note is hidden and when the application exits.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the note is saved.
    /// </returns>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        await SaveIfDirtyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer.Dispose();
        _saveGate.Dispose();
    }

    /// <summary>
    /// Runs the delayed save. A failure cannot be thrown from a timer callback (it would crash the process), so it is
    /// raised through <see cref="SaveFailed"/> instead.
    /// </summary>
    /// <param name="state">
    /// Unused timer state.
    /// </param>
    private async void OnTimerElapsed(object? state)
    {
        try
        {
            await SaveIfDirtyAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            SaveFailed?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Saves the text if it changed since the last successful save. On failure the text stays marked as unsaved.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the save has finished or was not needed.
    /// </returns>
    private async Task SaveIfDirtyAsync(CancellationToken cancellationToken)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            string snapshot;
            lock (_stateLock)
            {
                if (!_isDirty)
                {
                    return;
                }

                snapshot = _text;
                _isDirty = false;
            }

            try
            {
                await _store.SaveAsync(snapshot, cancellationToken);
            }
            catch
            {
                lock (_stateLock)
                {
                    _isDirty = true;
                }

                throw;
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }
}
