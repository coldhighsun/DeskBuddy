using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Behavior;
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
    /// The brain under test.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetBrainTests"/> class.
    /// </summary>
    public PetBrainTests()
    {
        var options = new PetBehaviorOptions { SleepAfter = SleepAfter };
        _brain = new PetBrain(_idle, new PetBehaviorStateMachine(options, new FakeTimeProvider(), new Random(1)));
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
