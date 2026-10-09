using AwesomeAssertions;
using DeskBuddy.Domain.Todos;
using DeskBuddy.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskBuddy.Infrastructure.Tests.Storage;

/// <summary>
/// Tests for <see cref="JsonTodoRepository"/>, using a real temporary directory per test.
/// </summary>
public sealed class JsonTodoRepositoryTests : IDisposable
{
    /// <summary>
    /// Temporary data directory, unique per test instance. It does not exist until something is saved.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyTodos_{Guid.NewGuid():N}");

    /// <summary>
    /// The repository under test.
    /// </summary>
    private readonly JsonTodoRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonTodoRepositoryTests"/> class.
    /// </summary>
    public JsonTodoRepositoryTests()
    {
        _repository = new JsonTodoRepository(_directory, NullLogger<JsonTodoRepository>.Instance);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// Gets the path of the data file.
    /// </summary>
    private string DataPath => Path.Combine(_directory, JsonTodoRepository.FileName);

    /// <summary>
    /// With nothing saved yet the list is empty and no file or directory is created by reading.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NothingSaved_ReturnsEmptyWithoutCreatingFiles()
    {
        // Act
        var items = await _repository.LoadAsync();

        // Assert
        items.Should().BeEmpty();
        Directory.Exists(_directory).Should().BeFalse();
    }

    /// <summary>
    /// Saving creates the directory and the entries survive a round trip unchanged.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsItems()
    {
        // Arrange
        var created = new DateTimeOffset(2026, 10, 10, 8, 30, 0, TimeSpan.FromHours(8));
        TodoItem[] items =
        [
            new(Guid.NewGuid(), "open task", IsDone: false, created),
            new(Guid.NewGuid(), "done task", IsDone: true, created, created.AddHours(2)),
        ];

        // Act
        await _repository.SaveAsync(items);
        var loaded = await _repository.LoadAsync();

        // Assert
        loaded.Should().Equal(items);
    }

    /// <summary>
    /// Non-ASCII text, including emoji, survives the round trip.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_PreservesChineseAndEmoji()
    {
        // Arrange
        TodoItem[] items = [new(Guid.NewGuid(), "写周报 📝 ＆ <tag>", IsDone: false, DateTimeOffset.UtcNow)];

        // Act
        await _repository.SaveAsync(items);
        var loaded = await _repository.LoadAsync();

        // Assert
        loaded.Single().Title.Should().Be("写周报 📝 ＆ <tag>");
    }

    /// <summary>
    /// The file stores Chinese text literally rather than as \uXXXX escapes, so it can be read and edited by hand.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ChineseTitle_IsStoredLiterallyInFile()
    {
        // Act
        await _repository.SaveAsync([new TodoItem(Guid.NewGuid(), "订机票", IsDone: false, DateTimeOffset.UtcNow)]);

        // Assert
        var json = await File.ReadAllTextAsync(DataPath);
        json.Should().Contain("订机票");
        json.Should().NotContain("\\u");
    }

    /// <summary>
    /// Saving replaces the previous content completely.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Twice_KeepsOnlyLatest()
    {
        // Arrange
        await _repository.SaveAsync([new TodoItem(Guid.NewGuid(), "old", IsDone: false, DateTimeOffset.UtcNow)]);

        // Act
        await _repository.SaveAsync([]);

        // Assert
        (await _repository.LoadAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// A completed save leaves no temporary file behind.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Completed_LeavesNoTemporaryFile()
    {
        // Act
        await _repository.SaveAsync([new TodoItem(Guid.NewGuid(), "task", IsDone: false, DateTimeOffset.UtcNow)]);

        // Assert
        Directory.GetFiles(_directory).Select(Path.GetFileName).Should().Equal(JsonTodoRepository.FileName);
    }

    /// <summary>
    /// A file that is not valid JSON is set aside untouched and the list starts empty.
    /// </summary>
    [Fact]
    public async Task LoadAsync_CorruptFile_MovesItToBackupAndReturnsEmpty()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(DataPath, "{ this is not json");

        // Act
        var items = await _repository.LoadAsync();

        // Assert
        items.Should().BeEmpty();
        File.Exists(DataPath).Should().BeFalse();
        (await File.ReadAllTextAsync(DataPath + ".bak")).Should().Be("{ this is not json");
    }

    /// <summary>
    /// After quarantining a corrupt file, saving works normally and the backup is kept.
    /// </summary>
    [Fact]
    public async Task SaveAsync_AfterCorruptFile_WorksAndKeepsBackup()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(DataPath, "garbage");
        await _repository.LoadAsync();

        // Act
        await _repository.SaveAsync([new TodoItem(Guid.NewGuid(), "fresh", IsDone: false, DateTimeOffset.UtcNow)]);

        // Assert
        (await _repository.LoadAsync()).Should().ContainSingle();
        File.Exists(DataPath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// An empty file is treated as corrupt rather than as an empty list.
    /// </summary>
    [Fact]
    public async Task LoadAsync_EmptyFile_IsQuarantined()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(DataPath, string.Empty);

        // Act
        var items = await _repository.LoadAsync();

        // Assert
        items.Should().BeEmpty();
        File.Exists(DataPath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// Valid JSON of the wrong shape is treated as corrupt.
    /// </summary>
    [Fact]
    public async Task LoadAsync_JsonWithoutItems_IsQuarantined()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(DataPath, "{\"version\":1}");

        // Act
        var items = await _repository.LoadAsync();

        // Assert
        items.Should().BeEmpty();
        File.Exists(DataPath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// Entries without a title are dropped on load.
    /// </summary>
    [Fact]
    public async Task LoadAsync_EntryWithBlankTitle_IsSkipped()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        var good = Guid.NewGuid();
        var json = $$"""
            {"version":1,"items":[
              {"id":"{{good}}","title":"keep me","isDone":false,"createdAt":"2026-10-10T00:00:00+00:00","completedAt":null},
              {"id":"{{Guid.NewGuid()}}","title":"   ","isDone":false,"createdAt":"2026-10-10T00:00:00+00:00","completedAt":null}
            ]}
            """;
        await File.WriteAllTextAsync(DataPath, json);

        // Act
        var items = await _repository.LoadAsync();

        // Assert
        items.Should().ContainSingle().Which.Id.Should().Be(good);
    }
}
