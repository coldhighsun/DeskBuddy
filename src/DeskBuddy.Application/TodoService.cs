using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Todos;

namespace DeskBuddy.Application;

/// <summary>
/// Keeps the to-do list in memory and saves it after every change. Saves are serialized, and a failed save is reported
/// to the caller rather than swallowed. If the saved data could not be read at startup, changes are refused so the
/// unreadable data is never overwritten.
/// </summary>
/// <param name="repository">
/// Where the entries are saved.
/// </param>
/// <param name="timeProvider">
/// The time source for creation and completion stamps.
/// </param>
public sealed class TodoService(ITodoRepository repository, TimeProvider timeProvider)
{
    /// <summary>
    /// Makes sure only one change-and-save runs at a time.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// The current list.
    /// </summary>
    private TodoList _list = new(timeProvider);

    /// <summary>
    /// Raised after the list changed in memory (before the save finishes).
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets why loading failed at startup, or <see langword="null"/> if it succeeded. While set, the list is empty and
    /// every change is refused.
    /// </summary>
    public Exception? LoadFailure { get; private set; }

    /// <summary>
    /// Gets the entries in display order.
    /// </summary>
    public IReadOnlyList<TodoItem> Items => _list.Items;

    /// <summary>
    /// Loads the saved entries. A storage problem is recorded in <see cref="LoadFailure"/> instead of thrown, so the
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
            var items = await repository.LoadAsync(cancellationToken);
            _list = new TodoList(timeProvider, items);
            LoadFailure = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LoadFailure = ex;
        }
    }

    /// <summary>
    /// Adds an entry and saves.
    /// </summary>
    /// <param name="title">
    /// What needs doing.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The new entry, or <see langword="null"/> if the title was blank.
    /// </returns>
    public Task<TodoItem?> AddAsync(string title, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(list => list.Add(title), item => item is not null, cancellationToken);
    }

    /// <summary>
    /// Completes or reopens an entry and saves.
    /// </summary>
    /// <param name="id">
    /// The entry to toggle.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the entry existed.
    /// </returns>
    public Task<bool> ToggleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(list => list.Toggle(id), found => found, cancellationToken);
    }

    /// <summary>
    /// Deletes an entry and saves.
    /// </summary>
    /// <param name="id">
    /// The entry to delete.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the entry existed.
    /// </returns>
    public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(list => list.Remove(id), found => found, cancellationToken);
    }

    /// <summary>
    /// Deletes all completed entries and saves.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// How many entries were deleted.
    /// </returns>
    public Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        return ChangeAsync(list => list.ClearCompleted(), count => count > 0, cancellationToken);
    }

    /// <summary>
    /// Applies a change to the list and, if it changed anything, saves the result.
    /// </summary>
    /// <typeparam name="TResult">
    /// The result type of the change.
    /// </typeparam>
    /// <param name="change">
    /// The change to apply.
    /// </param>
    /// <param name="didChange">
    /// Tells from the result whether anything changed and therefore needs saving.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The result of the change.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when loading failed at startup, so changes are refused.
    /// </exception>
    private async Task<TResult> ChangeAsync<TResult>(
        Func<TodoList, TResult> change,
        Func<TResult, bool> didChange,
        CancellationToken cancellationToken)
    {
        if (LoadFailure is not null)
        {
            throw new InvalidOperationException(
                "The saved to-do list could not be read, so changes are refused to avoid overwriting it.",
                LoadFailure);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var result = change(_list);
            if (didChange(result))
            {
                Changed?.Invoke(this, EventArgs.Empty);
                await repository.SaveAsync(_list.Items, cancellationToken);
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
