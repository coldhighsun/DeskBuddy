using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="SettingsService"/>.
/// </summary>
public class SettingsServiceTests
{
    /// <summary>
    /// The fake storage the service saves to.
    /// </summary>
    private readonly FakeSettingsStore _store = new();

    /// <summary>
    /// The service under test.
    /// </summary>
    private readonly SettingsService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsServiceTests"/> class.
    /// </summary>
    public SettingsServiceTests()
    {
        _service = new SettingsService(_store);
    }

    /// <summary>
    /// Before anything is loaded the defaults are in effect.
    /// </summary>
    [Fact]
    public void Current_BeforeInitialize_IsDefault()
    {
        // Assert
        _service.Current.Should().Be(PetSettings.Default);
    }

    /// <summary>
    /// Saved settings are available after initialization.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_SavedSettings_AreLoaded()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { ClimbEdges = true };

        // Act
        await _service.InitializeAsync(TestContext.Current.CancellationToken);

        // Assert
        _service.Current.ClimbEdges.Should().BeTrue();
        _service.LoadFailure.Should().BeNull();
    }

    /// <summary>
    /// Loaded values outside the allowed range are corrected.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_OutOfRangeSettings_AreNormalized()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { SleepAfter = TimeSpan.Zero };

        // Act
        await _service.InitializeAsync(TestContext.Current.CancellationToken);

        // Assert
        _service.Current.SleepAfter.Should().Be(PetSettings.MinSleepAfter);
    }

    /// <summary>
    /// An update is applied, announced and saved.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Change_AppliesRaisesChangedAndSaves()
    {
        // Arrange
        await _service.InitializeAsync(TestContext.Current.CancellationToken);
        PetSettings? announced = null;
        _service.Changed += (_, settings) => announced = settings;

        // Act
        var result = await _service.UpdateAsync(s => s with { ClimbEdges = true }, TestContext.Current.CancellationToken);

        // Assert
        result.ClimbEdges.Should().BeTrue();
        _service.Current.Should().Be(result);
        announced.Should().Be(result);
        _store.Stored.Should().Be(result);
        _store.SaveCount.Should().Be(1);
    }

    /// <summary>
    /// The result of an update is normalized before it is applied and saved.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_OutOfRangeValue_IsNormalizedBeforeSaving()
    {
        // Arrange
        await _service.InitializeAsync(TestContext.Current.CancellationToken);

        // Act
        await _service.UpdateAsync(s => s with { SleepAfter = TimeSpan.FromSeconds(1) }, TestContext.Current.CancellationToken);

        // Assert
        _service.Current.SleepAfter.Should().Be(PetSettings.MinSleepAfter);
        _store.Stored!.SleepAfter.Should().Be(PetSettings.MinSleepAfter);
    }

    /// <summary>
    /// A failed save is reported to the caller, and the change stays in effect in memory.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_SaveFails_ThrowsAndKeepsChangeInMemory()
    {
        // Arrange
        await _service.InitializeAsync(TestContext.Current.CancellationToken);
        _store.SaveException = new IOException("disk full");

        // Act
        var act = () => _service.UpdateAsync(s => s with { ClimbEdges = true });

        // Assert
        await act.Should().ThrowAsync<IOException>();
        _service.Current.ClimbEdges.Should().BeTrue();
    }

    /// <summary>
    /// A storage problem at startup is recorded and the defaults stay in effect.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_StorageUnreadable_RecordsFailureAndKeepsDefaults()
    {
        // Arrange
        _store.LoadException = new UnauthorizedAccessException("denied");

        // Act
        await _service.InitializeAsync(TestContext.Current.CancellationToken);

        // Assert
        _service.LoadFailure.Should().BeOfType<UnauthorizedAccessException>();
        _service.Current.Should().Be(PetSettings.Default);
    }

    /// <summary>
    /// After a failed load every update is refused and nothing is saved, so the unreadable file survives.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_AfterLoadFailure_IsRefusedAndNeverSaves()
    {
        // Arrange
        _store.LoadException = new IOException("locked");
        await _service.InitializeAsync(TestContext.Current.CancellationToken);

        // Act
        var act = () => _service.UpdateAsync(s => s with { ClimbEdges = true });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// Concurrent updates are all applied, one save at a time.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ConcurrentCalls_AreSerialized()
    {
        // Arrange
        await _service.InitializeAsync(TestContext.Current.CancellationToken);
        _store.SaveDelay = TimeSpan.FromMilliseconds(20);

        // Act
        await Task.WhenAll(Enumerable.Range(1, 8).Select(i =>
            _service.UpdateAsync(s => s with { BlacklistedProcesses = [.. s.BlacklistedProcesses, $"app{i}"] })));

        // Assert
        _service.Current.BlacklistedProcesses.Should().HaveCount(8);
        _store.MaxConcurrentSaves.Should().Be(1);
    }

    /// <summary>
    /// In-memory settings store with failure and timing hooks.
    /// </summary>
    private sealed class FakeSettingsStore : ISettingsStore
    {
        /// <summary>
        /// Number of saves currently running, to detect overlap.
        /// </summary>
        private int _running;

        /// <summary>
        /// Gets or sets the stored settings.
        /// </summary>
        public PetSettings? Stored { get; set; }

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
        public Task<PetSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return LoadException is null
                ? Task.FromResult(Stored ?? PetSettings.Default)
                : Task.FromException<PetSettings>(LoadException);
        }

        /// <inheritdoc />
        public async Task SaveAsync(PetSettings settings, CancellationToken cancellationToken = default)
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

                Stored = settings;
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }
    }

    /// <summary>
    /// Loading settings that break the rules for the process lists takes the offending names out and says which.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ListsBreakingTheRules_AreFixedAndReported()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { MeetingProcesses = ["Zoom", "Weixin"], BlacklistedProcesses = ["zoom", "Game", "My Game"] };

        // Act
        await _service.InitializeAsync(TestContext.Current.CancellationToken);

        // Assert
        _service.Current.BlacklistedProcesses.Should().Equal("Game");
        _service.Current.ChatProcesses.Should().NotContain("Weixin");
        _service.RemovedAtLoad.Select(item => item.Name).Should().BeEquivalentTo(["Weixin", "zoom", "My Game"]);
    }

    /// <summary>
    /// Settings that follow the rules load with nothing reported.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_DefaultSettings_ReportNothing()
    {
        // Act
        await _service.InitializeAsync(TestContext.Current.CancellationToken);

        // Assert
        _service.RemovedAtLoad.Should().BeEmpty();
    }

    /// <summary>
    /// A change that would put a process in two lists is made to follow the rules before it is stored.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ChangeBreakingTheListRules_IsEnforced()
    {
        // Arrange
        await _service.InitializeAsync(TestContext.Current.CancellationToken);

        // Act
        await _service.UpdateAsync(s => s with { MeetingProcesses = ["Zoom", "Signal"], ChatProcesses = ["signal", "Weixin"] }, TestContext.Current.CancellationToken);

        // Assert
        _service.Current.MeetingProcesses.Should().Equal("Zoom", "Signal");
        _service.Current.ChatProcesses.Should().Equal("Weixin");
    }
}
