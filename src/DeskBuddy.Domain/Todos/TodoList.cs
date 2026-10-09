namespace DeskBuddy.Domain.Todos;

/// <summary>
/// The user's to-do list: adding, completing, removing and clearing entries.
/// </summary>
public sealed class TodoList
{
    /// <summary>
    /// The longest allowed title, in characters.
    /// </summary>
    public const int MaxTitleLength = 200;

    /// <summary>
    /// Source of the current time, for creation and completion stamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The entries in the order they were added.
    /// </summary>
    private readonly List<TodoItem> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoList"/> class.
    /// </summary>
    /// <param name="timeProvider">
    /// The time source.
    /// </param>
    /// <param name="existing">
    /// Entries to start with, for example loaded from storage; <see langword="null"/> for an empty list.
    /// </param>
    public TodoList(TimeProvider timeProvider, IEnumerable<TodoItem>? existing = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _items = existing is null ? [] : [.. existing];
    }

    /// <summary>
    /// Gets the entries in display order: open ones first, oldest first; then completed ones, most recently completed
    /// first.
    /// </summary>
    public IReadOnlyList<TodoItem> Items =>
    [
        .. _items.Where(item => !item.IsDone).OrderBy(item => item.CreatedAt),
        .. _items.Where(item => item.IsDone).OrderByDescending(item => item.CompletedAt ?? item.CreatedAt),
    ];

    /// <summary>
    /// Adds an open entry.
    /// </summary>
    /// <param name="title">
    /// What needs doing. Surrounding whitespace is trimmed.
    /// </param>
    /// <returns>
    /// The new entry, or <see langword="null"/> if the title is empty or only whitespace.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the trimmed title is longer than <see cref="MaxTitleLength"/>.
    /// </exception>
    public TodoItem? Add(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException($"A title can be at most {MaxTitleLength} characters.", nameof(title));
        }

        var item = new TodoItem(Guid.NewGuid(), trimmed, IsDone: false, _timeProvider.GetUtcNow());
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Completes an open entry, or reopens a completed one.
    /// </summary>
    /// <param name="id">
    /// The entry to toggle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the entry exists.
    /// </returns>
    public bool Toggle(Guid id)
    {
        var index = _items.FindIndex(item => item.Id == id);
        if (index < 0)
        {
            return false;
        }

        var item = _items[index];
        _items[index] = item.IsDone
            ? item with { IsDone = false, CompletedAt = null }
            : item with { IsDone = true, CompletedAt = _timeProvider.GetUtcNow() };
        return true;
    }

    /// <summary>
    /// Deletes an entry.
    /// </summary>
    /// <param name="id">
    /// The entry to delete.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the entry existed.
    /// </returns>
    public bool Remove(Guid id)
    {
        return _items.RemoveAll(item => item.Id == id) > 0;
    }

    /// <summary>
    /// Deletes every completed entry.
    /// </summary>
    /// <returns>
    /// How many entries were deleted.
    /// </returns>
    public int ClearCompleted()
    {
        return _items.RemoveAll(item => item.IsDone);
    }
}
