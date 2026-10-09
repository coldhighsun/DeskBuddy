using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Todos;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="TodoService"/>.
/// </summary>
public class TodoServiceTests
{
    /// <summary>
    /// The fake storage the service saves to.
    /// </summary>
    private readonly FakeTodoRepository _repository = new();

    /// <summary>
    /// The clock shared with the service.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The service under test.
    /// </summary>
    private readonly TodoService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoServiceTests"/> class.
    /// </summary>
    public TodoServiceTests()
    {
        _service = new TodoService(_repository, _clock);
    }

    /// <summary>
    /// Entries saved earlier are available after initialization.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_SavedItems_AreLoaded()
    {
        // Arrange
        var saved = new TodoItem(Guid.NewGuid(), "from disk", IsDone: false, _clock.GetUtcNow());
        _repository.Stored = [saved];

        // Act
        await _service.InitializeAsync();

        // Assert
        _service.Items.Should().ContainSingle().Which.Should().Be(saved);
        _service.LoadFailure.Should().BeNull();
    }

    /// <summary>
    /// Initialization announces the loaded list so a view can show it.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_RaisesChanged()
    {
        // Arrange
        var raised = 0;
        _service.Changed += (_, _) => raised++;

        // Act
        await _service.InitializeAsync();

        // Assert
        raised.Should().Be(1);
    }

    /// <summary>
    /// Adding saves the complete list.
    /// </summary>
    [Fact]
    public async Task AddAsync_ValidTitle_AddsAndSaves()
    {
        // Arrange
        await _service.InitializeAsync();

        // Act
        var item = await _service.AddAsync("write tests");

        // Assert
        item.Should().NotBeNull();
        _service.Items.Should().ContainSingle();
        _repository.SaveCount.Should().Be(1);
        _repository.Stored.Should().ContainSingle().Which.Title.Should().Be("write tests");
    }

    /// <summary>
    /// A blank title adds nothing and does not touch storage.
    /// </summary>
    [Fact]
    public async Task AddAsync_BlankTitle_DoesNotSave()
    {
        // Arrange
        await _service.InitializeAsync();

        // Act
        var item = await _service.AddAsync("   ");

        // Assert
        item.Should().BeNull();
        _repository.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// Toggling saves, and toggling an unknown entry does not.
    /// </summary>
    [Fact]
    public async Task ToggleAsync_KnownAndUnknownId_SavesOnlyWhenFound()
    {
        // Arrange
        await _service.InitializeAsync();
        var item = (await _service.AddAsync("task"))!;
        _repository.SaveCount = 0;

        // Act
        var unknown = await _service.ToggleAsync(Guid.NewGuid());
        var known = await _service.ToggleAsync(item.Id);

        // Assert
        unknown.Should().BeFalse();
        known.Should().BeTrue();
        _repository.SaveCount.Should().Be(1);
        _repository.Stored.Single().IsDone.Should().BeTrue();
    }

    /// <summary>
    /// Removing saves the shorter list.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_ExistingItem_RemovesAndSaves()
    {
        // Arrange
        await _service.InitializeAsync();
        var item = (await _service.AddAsync("task"))!;

        // Act
        var removed = await _service.RemoveAsync(item.Id);

        // Assert
        removed.Should().BeTrue();
        _service.Items.Should().BeEmpty();
        _repository.Stored.Should().BeEmpty();
    }

    /// <summary>
    /// Clearing completed entries saves only when something was cleared.
    /// </summary>
    [Fact]
    public async Task ClearCompletedAsync_OnlySavesWhenSomethingCleared()
    {
        // Arrange
        await _service.InitializeAsync();
        var item = (await _service.AddAsync("task"))!;
        _repository.SaveCount = 0;

        // Act
        var none = await _service.ClearCompletedAsync();
        await _service.ToggleAsync(item.Id);
        _repository.SaveCount = 0;
        var one = await _service.ClearCompletedAsync();

        // Assert
        none.Should().Be(0);
        one.Should().Be(1);
        _repository.SaveCount.Should().Be(1);
        _repository.Stored.Should().BeEmpty();
    }

    /// <summary>
    /// Every change announces itself so a view can refresh.
    /// </summary>
    [Fact]
    public async Task Changed_AfterAdd_IsRaised()
    {
        // Arrange
        await _service.InitializeAsync();
        var raised = 0;
        _service.Changed += (_, _) => raised++;

        // Act
        await _service.AddAsync("task");

        // Assert
        raised.Should().Be(1);
    }

    /// <summary>
    /// A failed save is reported to the caller, not swallowed, and the change stays in memory so a later save keeps it.
    /// </summary>
    [Fact]
    public async Task AddAsync_SaveFails_ThrowsAndKeepsItemInMemory()
    {
        // Arrange
        await _service.InitializeAsync();
        _repository.SaveException = new IOException("disk full");

        // Act
        var act = () => _service.AddAsync("task");

        // Assert
        await act.Should().ThrowAsync<IOException>();
        _service.Items.Should().ContainSingle();
    }

    /// <summary>
    /// A storage problem at startup is recorded instead of crashing the application.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_StorageUnreadable_RecordsFailure()
    {
        // Arrange
        _repository.LoadException = new IOException("locked");

        // Act
        await _service.InitializeAsync();

        // Assert
        _service.LoadFailure.Should().BeOfType<IOException>();
        _service.Items.Should().BeEmpty();
    }

    /// <summary>
    /// After a failed load every change is refused, so the unreadable data cannot be overwritten.
    /// </summary>
    [Fact]
    public async Task AddAsync_AfterLoadFailure_IsRefusedAndNeverSaves()
    {
        // Arrange
        _repository.LoadException = new UnauthorizedAccessException("denied");
        await _service.InitializeAsync();

        // Act
        var act = () => _service.AddAsync("task");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _repository.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// Rapid concurrent changes are all kept and all saved, one at a time.
    /// </summary>
    [Fact]
    public async Task AddAsync_ConcurrentCalls_AllAddedAndSavedSerially()
    {
        // Arrange
        await _service.InitializeAsync();
        _repository.SaveDelay = TimeSpan.FromMilliseconds(20);

        // Act
        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => _service.AddAsync($"task {i}")));

        // Assert
        _service.Items.Should().HaveCount(10);
        _repository.Stored.Should().HaveCount(10);
        _repository.MaxConcurrentSaves.Should().Be(1);
    }

    /// <summary>
    /// In-memory repository with failure and timing hooks.
    /// </summary>
    private sealed class FakeTodoRepository : ITodoRepository
    {
        /// <summary>
        /// Number of saves currently running, to detect overlap.
        /// </summary>
        private int _running;

        /// <summary>
        /// Gets or sets the stored entries.
        /// </summary>
        public IReadOnlyList<TodoItem> Stored { get; set; } = [];

        /// <summary>
        /// Gets or sets how many times <see cref="SaveAsync"/> was called.
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

        /// <summary>
        /// Gets or sets an artificial delay inside <see cref="SaveAsync"/>.
        /// </summary>
        public TimeSpan SaveDelay { get; set; }

        /// <summary>
        /// Gets the highest number of overlapping saves observed.
        /// </summary>
        public int MaxConcurrentSaves { get; private set; }

        /// <inheritdoc />
        public Task<IReadOnlyList<TodoItem>> LoadAsync(CancellationToken cancellationToken = default)
        {
            return LoadException is null ? Task.FromResult(Stored) : Task.FromException<IReadOnlyList<TodoItem>>(LoadException);
        }

        /// <inheritdoc />
        public async Task SaveAsync(IReadOnlyList<TodoItem> items, CancellationToken cancellationToken = default)
        {
            var running = Interlocked.Increment(ref _running);
            MaxConcurrentSaves = Math.Max(MaxConcurrentSaves, running);
            try
            {
                if (SaveDelay > TimeSpan.Zero)
                {
                    await Task.Delay(SaveDelay, cancellationToken);
                }

                SaveCount++;
                if (SaveException is not null)
                {
                    throw SaveException;
                }

                Stored = items;
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }
    }
}
