namespace DeskBuddy.Domain.Todos;

/// <summary>
/// A single to-do entry.
/// </summary>
/// <param name="Id">
/// Unique identifier.
/// </param>
/// <param name="Title">
/// What needs doing.
/// </param>
/// <param name="IsDone">
/// Whether the entry has been completed.
/// </param>
/// <param name="CreatedAt">
/// When the entry was added.
/// </param>
/// <param name="CompletedAt">
/// When the entry was completed; <see langword="null"/> while it is still open.
/// </param>
public sealed record TodoItem(
    Guid Id,
    string Title,
    bool IsDone,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt = null);
