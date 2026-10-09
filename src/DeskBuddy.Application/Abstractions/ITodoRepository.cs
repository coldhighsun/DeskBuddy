using DeskBuddy.Domain.Todos;

namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Persists the user's to-do entries.
/// </summary>
public interface ITodoRepository
{
    /// <summary>
    /// Loads all saved entries.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The saved entries; empty if nothing has been saved yet.
    /// </returns>
    /// <exception cref="IOException">
    /// Thrown when the storage cannot be read.
    /// </exception>
    Task<IReadOnlyList<TodoItem>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the saved entries with the given ones.
    /// </summary>
    /// <param name="items">
    /// The complete list of entries to save.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <exception cref="IOException">
    /// Thrown when the storage cannot be written.
    /// </exception>
    Task SaveAsync(IReadOnlyList<TodoItem> items, CancellationToken cancellationToken = default);
}
