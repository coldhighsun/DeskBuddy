using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="NoteService"/>.
/// </summary>
public sealed class NoteServiceTests : IDisposable
{
    /// <summary>
    /// The save delay used by the tests.
    /// </summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// The fake storage the service saves to.
    /// </summary>
    private readonly FakeNoteStore _store = new();

    /// <summary>
    /// The clock that drives the save delay.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The service under test.
    /// </summary>
    private readonly NoteService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteServiceTests"/> class.
    /// </summary>
    public NoteServiceTests()
    {
        _service = new NoteService(_store, _clock, SaveDelay);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _service.Dispose();
    }

    /// <summary>
    /// The saved note is available after initialization.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_SavedNote_IsLoaded()
    {
        // Arrange
        _store.Stored = "remember the milk";

        // Act
        await _service.InitializeAsync();

        // Assert
        _service.Text.Should().Be("remember the milk");
    }

    /// <summary>
    /// Typing does not save immediately.
    /// </summary>
    [Fact]
    public async Task SetText_JustTyped_DoesNotSaveYet()
    {
        // Arrange
        await _service.InitializeAsync();

        // Act
        _service.SetText("hello");
        _clock.Advance(SaveDelay - TimeSpan.FromMilliseconds(1));

        // Assert
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// Once the user pauses for the delay, the note is saved.
    /// </summary>
    [Fact]
    public async Task SetText_AfterDelay_Saves()
    {
        // Arrange
        await _service.InitializeAsync();

        // Act
        _service.SetText("hello");
        _clock.Advance(SaveDelay);

        // Assert
        _store.SaveCount.Should().Be(1);
        _store.Stored.Should().Be("hello");
    }

    /// <summary>
    /// A burst of edits produces a single save, with the final text, once typing stops.
    /// </summary>
    [Fact]
    public async Task SetText_BurstOfEdits_SavesOnceWithFinalText()
    {
        // Arrange
        await _service.InitializeAsync();

        // Act
        foreach (var text in new[] { "h", "he", "hel", "hell", "hello" })
        {
            _service.SetText(text);
            _clock.Advance(TimeSpan.FromMilliseconds(300));
        }

        _clock.Advance(SaveDelay);

        // Assert
        _store.SaveCount.Should().Be(1);
        _store.Stored.Should().Be("hello");
    }

    /// <summary>
    /// Setting the same text again is not a change and schedules nothing.
    /// </summary>
    [Fact]
    public async Task SetText_SameText_DoesNotSave()
    {
        // Arrange
        _store.Stored = "same";
        await _service.InitializeAsync();

        // Act
        _service.SetText("same");
        _clock.Advance(SaveDelay * 2);

        // Assert
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// Flushing saves unsaved text immediately and cancels the pending delayed save.
    /// </summary>
    [Fact]
    public async Task FlushAsync_UnsavedText_SavesNowAndNotAgainLater()
    {
        // Arrange
        await _service.InitializeAsync();
        _service.SetText("urgent");

        // Act
        await _service.FlushAsync();
        _clock.Advance(SaveDelay * 2);

        // Assert
        _store.SaveCount.Should().Be(1);
        _store.Stored.Should().Be("urgent");
    }

    /// <summary>
    /// Flushing with nothing unsaved does not touch storage.
    /// </summary>
    [Fact]
    public async Task FlushAsync_NothingUnsaved_DoesNotSave()
    {
        // Arrange
        await _service.InitializeAsync();

        // Act
        await _service.FlushAsync();

        // Assert
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// A failed delayed save is raised as an event instead of crashing, and the text is retried on the next flush.
    /// </summary>
    [Fact]
    public async Task SetText_SaveFails_RaisesSaveFailedAndRetriesOnFlush()
    {
        // Arrange
        await _service.InitializeAsync();
        Exception? failure = null;
        _service.SaveFailed += (_, ex) => failure = ex;
        _store.SaveException = new IOException("disk full");
        _service.SetText("precious");

        // Act
        _clock.Advance(SaveDelay);
        _store.SaveException = null;
        await _service.FlushAsync();

        // Assert
        failure.Should().BeOfType<IOException>();
        _store.Stored.Should().Be("precious");
    }

    /// <summary>
    /// A failed explicit flush is thrown to the caller.
    /// </summary>
    [Fact]
    public async Task FlushAsync_SaveFails_Throws()
    {
        // Arrange
        await _service.InitializeAsync();
        _service.SetText("text");
        _store.SaveException = new IOException("disk full");

        // Act
        var act = () => _service.FlushAsync();

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    /// <summary>
    /// A storage problem at startup is recorded instead of crashing the application.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_StorageUnreadable_RecordsFailure()
    {
        // Arrange
        _store.LoadException = new IOException("locked");

        // Act
        await _service.InitializeAsync();

        // Assert
        _service.LoadFailure.Should().BeOfType<IOException>();
    }

    /// <summary>
    /// After a failed load edits are refused, so the unreadable note cannot be overwritten.
    /// </summary>
    [Fact]
    public async Task SetText_AfterLoadFailure_IsRefused()
    {
        // Arrange
        _store.LoadException = new IOException("locked");
        await _service.InitializeAsync();

        // Act
        var act = () => _service.SetText("overwrite");

        // Assert
        act.Should().Throw<InvalidOperationException>();
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// In-memory note store with failure hooks.
    /// </summary>
    private sealed class FakeNoteStore : INoteStore
    {
        /// <summary>
        /// Gets or sets the stored text.
        /// </summary>
        public string Stored { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets how many times <see cref="SaveAsync"/> succeeded.
        /// </summary>
        public int SaveCount { get; set; }

        /// <summary>
        /// Gets or sets an exception to throw from <see cref="LoadAsync"/>.
        /// </summary>
        public Exception? LoadException { get; set; }

        /// <summary>
        /// Gets or sets an exception to throw from <see cref="SaveAsync"/>.
        /// </summary>
        public Exception? SaveException { get; set; }

        /// <inheritdoc />
        public Task<string> LoadAsync(CancellationToken cancellationToken = default)
        {
            return LoadException is null ? Task.FromResult(Stored) : Task.FromException<string>(LoadException);
        }

        /// <inheritdoc />
        public Task SaveAsync(string text, CancellationToken cancellationToken = default)
        {
            if (SaveException is not null)
            {
                return Task.FromException(SaveException);
            }

            SaveCount++;
            Stored = text;
            return Task.CompletedTask;
        }
    }
}
