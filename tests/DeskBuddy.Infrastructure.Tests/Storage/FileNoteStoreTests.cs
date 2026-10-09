using AwesomeAssertions;
using DeskBuddy.Infrastructure.Storage;

namespace DeskBuddy.Infrastructure.Tests.Storage;

/// <summary>
/// Tests for <see cref="FileNoteStore"/>, using a real temporary directory per test.
/// </summary>
public sealed class FileNoteStoreTests : IDisposable
{
    /// <summary>
    /// Temporary data directory, unique per test instance. It does not exist until something is saved.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyNote_{Guid.NewGuid():N}");

    /// <summary>
    /// The store under test.
    /// </summary>
    private readonly FileNoteStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNoteStoreTests"/> class.
    /// </summary>
    public FileNoteStoreTests()
    {
        _store = new FileNoteStore(_directory);
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
    /// With nothing saved yet the note is empty.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NothingSaved_ReturnsEmpty()
    {
        // Act
        var text = await _store.LoadAsync();

        // Assert
        text.Should().BeEmpty();
    }

    /// <summary>
    /// Multi-line text with Chinese, emoji and trailing blank lines survives a round trip exactly.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsExactly()
    {
        // Arrange
        const string Text = "第一行\r\n第二行 📌\r\n\r\n  indented\r\n\r\n";

        // Act
        await _store.SaveAsync(Text);
        var loaded = await _store.LoadAsync();

        // Assert
        loaded.Should().Be(Text);
    }

    /// <summary>
    /// The file is plain UTF-8 without a byte order mark, so other editors read it cleanly.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WritesUtf8WithoutBom()
    {
        // Act
        await _store.SaveAsync("héllo");

        // Assert
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_directory, FileNoteStore.FileName));
        bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
        bytes.Should().Equal("héllo"u8.ToArray());
    }

    /// <summary>
    /// Saving replaces the previous note and leaves no temporary file behind.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Twice_KeepsOnlyLatestAndNoTemporaryFile()
    {
        // Arrange
        await _store.SaveAsync("old");

        // Act
        await _store.SaveAsync("new");

        // Assert
        (await _store.LoadAsync()).Should().Be("new");
        Directory.GetFiles(_directory).Select(Path.GetFileName).Should().Equal(FileNoteStore.FileName);
    }

    /// <summary>
    /// A file another process holds open for a moment (as antivirus does) is read once it is released.
    /// </summary>
    [Fact]
    public async Task LoadAsync_FileBrieflyLocked_RetriesAndSucceeds()
    {
        // Arrange
        await _store.SaveAsync("kept");
        var path = Path.Combine(_directory, FileNoteStore.FileName);
        using var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        _ = Task.Delay(TimeSpan.FromMilliseconds(250)).ContinueWith(_ => holder.Dispose(), TaskScheduler.Default);

        // Act
        var text = await _store.LoadAsync();

        // Assert
        text.Should().Be("kept");
    }

    /// <summary>
    /// A file locked for the whole retry period is reported to the caller instead of being waited on forever.
    /// </summary>
    [Fact]
    public async Task LoadAsync_FileLockedForGood_ThrowsIOException()
    {
        // Arrange
        await _store.SaveAsync("kept");
        var path = Path.Combine(_directory, FileNoteStore.FileName);
        using var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var act = () => _store.LoadAsync();

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    /// <summary>
    /// Saving over a file that is briefly held open by another process succeeds once it is released.
    /// </summary>
    [Fact]
    public async Task SaveAsync_FileBrieflyLocked_RetriesAndSucceeds()
    {
        // Arrange
        await _store.SaveAsync("old");
        var path = Path.Combine(_directory, FileNoteStore.FileName);
        using var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        _ = Task.Delay(TimeSpan.FromMilliseconds(250)).ContinueWith(_ => holder.Dispose(), TaskScheduler.Default);

        // Act
        await _store.SaveAsync("new");

        // Assert
        (await _store.LoadAsync()).Should().Be("new");
    }

    /// <summary>
    /// Saving an empty note clears the previous one.
    /// </summary>
    [Fact]
    public async Task SaveAsync_EmptyText_ClearsNote()
    {
        // Arrange
        await _store.SaveAsync("something");

        // Act
        await _store.SaveAsync(string.Empty);

        // Assert
        (await _store.LoadAsync()).Should().BeEmpty();
    }
}
