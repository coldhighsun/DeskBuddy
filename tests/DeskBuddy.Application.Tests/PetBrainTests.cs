using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.SystemLoad;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="PetBrain"/>.
/// </summary>
public class PetBrainTests
{
    /// <summary>
    /// Sleep threshold used by the test options.
    /// </summary>
    private static readonly TimeSpan SleepAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The fake idle sensor the brain reads.
    /// </summary>
    private readonly FakeIdleDetector _idle = new();

    /// <summary>
    /// The fake load sensor the brain reads.
    /// </summary>
    private readonly FakeLoadMonitor _load = new();

    /// <summary>
    /// The clock shared by the state machine and the load policy.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The brain under test.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetBrainTests"/> class.
    /// </summary>
    public PetBrainTests()
    {
        var options = new PetBehaviorOptions { SleepAfter = SleepAfter };
        var loadPolicy = new LoadReactionPolicy(new SystemLoadOptions(), _clock);
        _brain = new PetBrain(_idle, _load, loadPolicy, new PetBehaviorStateMachine(options, _clock, new Random(1)));
    }

    /// <summary>
    /// Load that stays high long enough makes the pet tired.
    /// </summary>
    [Fact]
    public void Tick_SustainedHighLoad_PetBecomesTired()
    {
        // Arrange
        _load.Snapshot = new SystemLoadSnapshot(CpuPercent: 99, MemoryPercent: 50);

        // Act
        PetState state = PetState.Idle;
        for (var i = 0; i < 12; i++)
        {
            state = _brain.Tick();
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// A momentary load spike does not make the pet tired.
    /// </summary>
    [Fact]
    public void Tick_BriefLoadSpike_PetStaysIdle()
    {
        // Arrange
        _load.Snapshot = new SystemLoadSnapshot(CpuPercent: 99, MemoryPercent: 50);

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// After the load subsides, the pet recovers.
    /// </summary>
    [Fact]
    public void Tick_LoadSubsides_PetRecovers()
    {
        // Arrange
        _load.Snapshot = new SystemLoadSnapshot(CpuPercent: 99, MemoryPercent: 50);
        for (var i = 0; i < 12; i++)
        {
            _brain.Tick();
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        _load.Snapshot = new SystemLoadSnapshot(CpuPercent: 5, MemoryPercent: 50);

        // Act
        PetState state = PetState.Tired;
        for (var i = 0; i < 12; i++)
        {
            state = _brain.Tick();
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// The brain feeds the sensor's idle time into the state machine.
    /// </summary>
    [Fact]
    public void Tick_UserAway_PetSleeps()
    {
        // Arrange
        _idle.IdleTime = SleepAfter;

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Sleep);
        _brain.State.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// With an active user the pet does not sleep.
    /// </summary>
    [Fact]
    public void Tick_UserActive_PetStaysIdle()
    {
        // Arrange
        _idle.IdleTime = TimeSpan.FromSeconds(1);

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Drag notifications reach the state machine.
    /// </summary>
    [Fact]
    public void OnDragStartedAndEnded_Forwarded_ToStateMachine()
    {
        // Act & Assert
        _brain.OnDragStarted();
        _brain.State.Should().Be(PetState.Drag);

        _brain.OnDragEnded();
        _brain.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Click notifications reach the state machine.
    /// </summary>
    [Fact]
    public void OnClicked_Forwarded_PetIsHappy()
    {
        // Act
        _brain.OnClicked();

        // Assert
        _brain.State.Should().Be(PetState.Happy);
    }

    /// <summary>
    /// Load sensor stub with a settable value.
    /// </summary>
    private sealed class FakeLoadMonitor : ISystemLoadMonitor
    {
        /// <summary>
        /// Gets or sets the reading to report.
        /// </summary>
        public SystemLoadSnapshot Snapshot { get; set; }

        /// <inheritdoc />
        public SystemLoadSnapshot GetSnapshot()
        {
            return Snapshot;
        }
    }

    /// <summary>
    /// Idle sensor stub with a settable value.
    /// </summary>
    private sealed class FakeIdleDetector : IIdleDetector
    {
        /// <summary>
        /// Gets or sets the idle time to report.
        /// </summary>
        public TimeSpan IdleTime { get; set; }

        /// <inheritdoc />
        public TimeSpan GetIdleTime()
        {
            return IdleTime;
        }
    }
}
