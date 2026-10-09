using AwesomeAssertions;
using DeskBuddy.Domain.Pomodoro;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Pomodoro;

/// <summary>
/// Tests for <see cref="PomodoroSession"/>. Options: work 25 min, short break 5 min, long break 15 min, long break
/// every 4th round, stale after 5 min.
/// </summary>
public class PomodoroSessionTests
{
    /// <summary>
    /// Work phase length used by the tests.
    /// </summary>
    private static readonly TimeSpan Work = TimeSpan.FromMinutes(25);

    /// <summary>
    /// Short break length used by the tests.
    /// </summary>
    private static readonly TimeSpan ShortBreak = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Long break length used by the tests.
    /// </summary>
    private static readonly TimeSpan LongBreak = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The fake clock shared with the session under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The session under test.
    /// </summary>
    private readonly PomodoroSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="PomodoroSessionTests"/> class.
    /// </summary>
    public PomodoroSessionTests()
    {
        _session = new PomodoroSession(new PomodoroOptions(), _clock);
    }

    /// <summary>
    /// Runs one full work phase, ending exactly on time, and returns the resulting transition.
    /// </summary>
    /// <returns>
    /// The transition produced when the work phase ended.
    /// </returns>
    private PomodoroTransition FinishWork()
    {
        _clock.Advance(Work);
        return _session.Update();
    }

    /// <summary>
    /// A new session is stopped with nothing remaining.
    /// </summary>
    [Fact]
    public void Constructor_Default_IsStopped()
    {
        // Assert
        _session.Phase.Should().Be(PomodoroPhase.Stopped);
        _session.IsRunning.Should().BeFalse();
        _session.Remaining.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// Starting begins a full work phase.
    /// </summary>
    [Fact]
    public void Start_WhenStopped_BeginsWorkPhase()
    {
        // Act
        _session.Start();

        // Assert
        _session.Phase.Should().Be(PomodoroPhase.Work);
        _session.IsRunning.Should().BeTrue();
        _session.Remaining.Should().Be(Work);
    }

    /// <summary>
    /// Starting while a phase is running does not restart it.
    /// </summary>
    [Fact]
    public void Start_WhenRunning_IsIgnored()
    {
        // Arrange
        _session.Start();
        _clock.Advance(TimeSpan.FromMinutes(10));

        // Act
        _session.Start();

        // Assert
        _session.Remaining.Should().Be(Work - TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// The remaining time counts down with the clock.
    /// </summary>
    [Fact]
    public void Remaining_AfterTimePasses_Decreases()
    {
        // Arrange
        _session.Start();

        // Act
        _clock.Advance(TimeSpan.FromMinutes(7));

        // Assert
        _session.Remaining.Should().Be(TimeSpan.FromMinutes(18));
    }

    /// <summary>
    /// Updating before the phase ends changes nothing.
    /// </summary>
    [Fact]
    public void Update_BeforePhaseEnd_ReportsNoChange()
    {
        // Arrange
        _session.Start();
        _clock.Advance(Work - TimeSpan.FromSeconds(1));

        // Act
        var transition = _session.Update();

        // Assert
        transition.Kind.Should().Be(PomodoroTransitionKind.None);
        _session.Phase.Should().Be(PomodoroPhase.Work);
    }

    /// <summary>
    /// A finished work phase flows straight into a short break.
    /// </summary>
    [Fact]
    public void Update_WorkEnds_StartsShortBreak()
    {
        // Arrange
        _session.Start();

        // Act
        var transition = FinishWork();

        // Assert
        transition.Should().Be(new PomodoroTransition(PomodoroTransitionKind.WorkCompleted));
        _session.Phase.Should().Be(PomodoroPhase.ShortBreak);
        _session.Remaining.Should().Be(ShortBreak);
        _session.CompletedWorkSessions.Should().Be(1);
    }

    /// <summary>
    /// When a break ends the session stops and waits for the user.
    /// </summary>
    [Fact]
    public void Update_BreakEnds_StopsAndWaitsForUser()
    {
        // Arrange
        _session.Start();
        FinishWork();
        _clock.Advance(ShortBreak);

        // Act
        var transition = _session.Update();

        // Assert
        transition.Should().Be(new PomodoroTransition(PomodoroTransitionKind.BreakCompleted));
        _session.Phase.Should().Be(PomodoroPhase.Stopped);
        _session.CompletedWorkSessions.Should().Be(1);
    }

    /// <summary>
    /// The fourth completed work phase is followed by a long break.
    /// </summary>
    [Fact]
    public void Update_FourthWorkEnds_StartsLongBreak()
    {
        // Arrange: three full rounds.
        for (var round = 0; round < 3; round++)
        {
            _session.Start();
            FinishWork();
            _clock.Advance(ShortBreak);
            _session.Update();
        }

        _session.Start();

        // Act
        FinishWork();

        // Assert
        _session.Phase.Should().Be(PomodoroPhase.LongBreak);
        _session.Remaining.Should().Be(LongBreak);
        _session.CompletedWorkSessions.Should().Be(4);
    }

    /// <summary>
    /// A finished long break closes the cycle, so the count starts over.
    /// </summary>
    [Fact]
    public void Update_LongBreakEnds_ResetsCycleCount()
    {
        // Arrange
        for (var round = 0; round < 3; round++)
        {
            _session.Start();
            FinishWork();
            _clock.Advance(ShortBreak);
            _session.Update();
        }

        _session.Start();
        FinishWork();
        _clock.Advance(LongBreak);

        // Act
        _session.Update();

        // Assert
        _session.Phase.Should().Be(PomodoroPhase.Stopped);
        _session.CompletedWorkSessions.Should().Be(0);
    }

    /// <summary>
    /// Pausing freezes the remaining time however long the pause lasts.
    /// </summary>
    [Fact]
    public void Pause_TimePasses_RemainingStaysFrozen()
    {
        // Arrange
        _session.Start();
        _clock.Advance(TimeSpan.FromMinutes(10));

        // Act
        _session.Pause();
        _clock.Advance(TimeSpan.FromHours(2));

        // Assert
        _session.IsPaused.Should().BeTrue();
        _session.IsRunning.Should().BeFalse();
        _session.Remaining.Should().Be(TimeSpan.FromMinutes(15));
        _session.Update().Kind.Should().Be(PomodoroTransitionKind.None);
    }

    /// <summary>
    /// Resuming continues from where the pause left off.
    /// </summary>
    [Fact]
    public void Resume_AfterPause_ContinuesFromRemaining()
    {
        // Arrange
        _session.Start();
        _clock.Advance(TimeSpan.FromMinutes(10));
        _session.Pause();
        _clock.Advance(TimeSpan.FromHours(2));

        // Act
        _session.Resume();
        _clock.Advance(TimeSpan.FromMinutes(5));

        // Assert
        _session.IsRunning.Should().BeTrue();
        _session.Remaining.Should().Be(TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// Pausing or resuming when there is nothing to pause or resume does nothing.
    /// </summary>
    [Fact]
    public void PauseAndResume_WhenStopped_AreIgnored()
    {
        // Act
        _session.Pause();
        _session.Resume();

        // Assert
        _session.Phase.Should().Be(PomodoroPhase.Stopped);
        _session.IsPaused.Should().BeFalse();
    }

    /// <summary>
    /// Skipping work counts it as completed and starts the break right away.
    /// </summary>
    [Fact]
    public void Skip_DuringWork_CountsItAndStartsBreak()
    {
        // Arrange
        _session.Start();
        _clock.Advance(TimeSpan.FromMinutes(3));

        // Act
        _session.Skip();

        // Assert
        _session.Phase.Should().Be(PomodoroPhase.ShortBreak);
        _session.Remaining.Should().Be(ShortBreak);
        _session.CompletedWorkSessions.Should().Be(1);
    }

    /// <summary>
    /// Skipping a break stops the session.
    /// </summary>
    [Fact]
    public void Skip_DuringBreak_Stops()
    {
        // Arrange
        _session.Start();
        FinishWork();

        // Act
        _session.Skip();

        // Assert
        _session.Phase.Should().Be(PomodoroPhase.Stopped);
    }

    /// <summary>
    /// Stopping discards the progress of the cycle.
    /// </summary>
    [Fact]
    public void Stop_AfterProgress_ResetsEverything()
    {
        // Arrange
        _session.Start();
        FinishWork();
        _session.Pause();

        // Act
        _session.Stop();

        // Assert
        _session.Phase.Should().Be(PomodoroPhase.Stopped);
        _session.IsPaused.Should().BeFalse();
        _session.CompletedWorkSessions.Should().Be(0);
    }

    /// <summary>
    /// A work phase that expired long ago (the machine slept) stops silently instead of starting a stale break.
    /// </summary>
    [Fact]
    public void Update_WorkExpiredLongAgo_StopsSilentlyAsStale()
    {
        // Arrange
        _session.Start();
        _clock.Advance(Work + TimeSpan.FromHours(3));

        // Act
        var transition = _session.Update();

        // Assert
        transition.Should().Be(new PomodoroTransition(PomodoroTransitionKind.WorkCompleted, IsStale: true));
        _session.Phase.Should().Be(PomodoroPhase.Stopped);
        _session.CompletedWorkSessions.Should().Be(1);
    }

    /// <summary>
    /// A break that expired long ago is reported as stale.
    /// </summary>
    [Fact]
    public void Update_BreakExpiredLongAgo_ReportsStale()
    {
        // Arrange
        _session.Start();
        FinishWork();
        _clock.Advance(ShortBreak + TimeSpan.FromHours(3));

        // Act
        var transition = _session.Update();

        // Assert
        transition.Should().Be(new PomodoroTransition(PomodoroTransitionKind.BreakCompleted, IsStale: true));
        _session.Phase.Should().Be(PomodoroPhase.Stopped);
    }

    /// <summary>
    /// A phase that ended only slightly late is still a fresh completion.
    /// </summary>
    [Fact]
    public void Update_WorkEndedSlightlyLate_IsNotStale()
    {
        // Arrange
        _session.Start();
        _clock.Advance(Work + TimeSpan.FromMinutes(1));

        // Act
        var transition = _session.Update();

        // Assert
        transition.IsStale.Should().BeFalse();
        _session.Phase.Should().Be(PomodoroPhase.ShortBreak);
        _session.Remaining.Should().Be(ShortBreak);
    }
}
