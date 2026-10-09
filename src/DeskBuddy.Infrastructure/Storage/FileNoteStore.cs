using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.Infrastructure.Storage;

/// <summary>
/// Stores the sticky note as plain UTF-8 text in <c>note.txt</c>, so it can also be read or edited outside the app.
/// </summary>
/// <param name="directory">
/// The directory that holds the data files.
/// </param>
public sealed class FileNoteStore(string directory) : INoteStore
{
    /// <summary>
    /// File name of the saved note.
    /// </summary>
    public const string FileName = "note.txt";

    /// <summary>
    /// Full path of the data file.
    /// </summary>
    private readonly string _path = Path.Combine(directory, FileName);

    /// <inheritdoc />
    public async Task<string> LoadAsync(CancellationToken cancellationToken = default)
    {
        return File.Exists(_path) ? await AtomicFile.ReadAllTextAsync(_path, cancellationToken) : string.Empty;
    }

    /// <inheritdoc />
    public Task SaveAsync(string text, CancellationToken cancellationToken = default)
    {
        return AtomicFile.WriteAllTextAsync(_path, text, cancellationToken);
    }
}
