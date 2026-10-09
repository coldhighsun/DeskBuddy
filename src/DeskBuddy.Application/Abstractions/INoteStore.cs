namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Persists the user's sticky note text.
/// </summary>
public interface INoteStore
{
    /// <summary>
    /// Loads the saved note.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The saved text; empty if nothing has been saved yet.
    /// </returns>
    /// <exception cref="IOException">
    /// Thrown when the storage cannot be read.
    /// </exception>
    Task<string> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the saved note.
    /// </summary>
    /// <param name="text">
    /// The complete note text.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <exception cref="IOException">
    /// Thrown when the storage cannot be written.
    /// </exception>
    Task SaveAsync(string text, CancellationToken cancellationToken = default);
}
