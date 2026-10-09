using System.Text.Encodings.Web;
using System.Text.Json;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Todos;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.Infrastructure.Storage;

/// <summary>
/// Stores the to-do list as <c>todos.json</c> in a directory. A file that cannot be parsed is set aside as
/// <c>todos.json.bak</c> and the list starts empty, so a corrupt file is never silently overwritten.
/// </summary>
/// <param name="directory">
/// The directory that holds the data files.
/// </param>
/// <param name="logger">
/// Logger used to report a quarantined file.
/// </param>
public sealed class JsonTodoRepository(string directory, ILogger<JsonTodoRepository> logger) : ITodoRepository
{
    /// <summary>
    /// File name of the saved list.
    /// </summary>
    public const string FileName = "todos.json";

    /// <summary>
    /// Version number written into the file, for future format changes.
    /// </summary>
    private const int FormatVersion = 1;

    /// <summary>
    /// Serializer options for the file.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // Keep Chinese and emoji readable in the file instead of \uXXXX escapes; the file is UTF-8 JSON, so this is safe.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Full path of the data file.
    /// </summary>
    private readonly string _path = Path.Combine(directory, FileName);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TodoItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        var json = await AtomicFile.ReadAllTextAsync(_path, cancellationToken);
        TodoFile? file;
        try
        {
            file = JsonSerializer.Deserialize<TodoFile>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            QuarantineCorruptFile(ex);
            return [];
        }

        if (file?.Items is null)
        {
            QuarantineCorruptFile(null);
            return [];
        }

        return [.. file.Items.Where(item => !string.IsNullOrWhiteSpace(item.Title))];
    }

    /// <inheritdoc />
    public Task SaveAsync(IReadOnlyList<TodoItem> items, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(new TodoFile(FormatVersion, [.. items]), SerializerOptions);
        return AtomicFile.WriteAllTextAsync(_path, json, cancellationToken);
    }

    /// <summary>
    /// Moves an unreadable data file aside so the next save does not destroy it.
    /// </summary>
    /// <param name="cause">
    /// The parse error, if any.
    /// </param>
    private void QuarantineCorruptFile(Exception? cause)
    {
        var backupPath = _path + ".bak";
        File.Move(_path, backupPath, overwrite: true);
        logger.LogWarning(cause, "The to-do file was unreadable and has been moved to {BackupPath}.", backupPath);
    }

    /// <summary>
    /// Shape of the JSON file.
    /// </summary>
    /// <param name="Version">
    /// Format version.
    /// </param>
    /// <param name="Items">
    /// The saved entries.
    /// </param>
    private sealed record TodoFile(int Version, List<TodoItem> Items);
}
