using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Pomodoro;
using DeskBuddy.Domain.Sedentary;
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
    /// Work phase length of the test pomodoro.
    /// </summary>
    private static readonly TimeSpan Work = TimeSpan.FromMinutes(25);

    /// <summary>
    /// Short break length of the test pomodoro.
    /// </summary>
    private static readonly TimeSpan ShortBreak = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The fake idle sensor the brain reads.
    /// </summary>
    private readonly FakeIdleDetector _idle = new();

    /// <summary>
    /// The fake load sensor the brain reads.
    /// </summary>
    private readonly FakeLoadMonitor _load = new();

    /// <summary>
    /// The fake do-not-disturb detector the brain reads.
    /// </summary>
    private readonly FakeFullscreenDetector _fullscreen = new();

    /// <summary>
    /// The clock shared by every time-dependent component.
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
        var behavior = new PetBehaviorOptions { SleepAfter = SleepAfter, MessageDuration = TimeSpan.FromSeconds(30) };
        var sedentary = new SedentaryOptions
        {
            ReminderAfter = TimeSpan.FromMinutes(10),
            RestThreshold = TimeSpan.FromMinutes(5),
            SnoozeDuration = TimeSpan.FromMinutes(3),
        };
        _brain = new PetBrain(
            _idle,
            _load,
            new LoadReactionPolicy(new SystemLoadOptions(), _clock),
            _fullscreen,
            new PomodoroSession(new PomodoroOptions(), _clock),
            new SedentaryReminderPolicy(sedentary, _clock),
            new PetMessageQueue(behavior.MessageDuration, _clock),
            new PetBehaviorStateMachine(behavior, _clock, new Random(1)));
    }

    /// <summary>
    /// Ticks the brain once a minute for the given number of minutes and returns the message on screen right after
    /// the tick that first showed one, or <see langword="null"/> if none appeared.
    /// </summary>
    /// <param name="minutes">
    /// How many minutes to run.
    /// </param>
    /// <returns>
    /// The first message that appeared, if any.
    /// </returns>
    private PetMessage? RunMinutesUntilMessage(int minutes)
    {
        for (var minute = 0; minute < minutes; minute++)
        {
            _brain.Tick();
            if (_brain.CurrentMessage is { } message)
            {
                return message;
            }

            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        return null;
    }

    /// <summary>
    /// A user who keeps working long enough is reminded to get up, and the pet calls for attention.
    /// </summary>
    [Fact]
    public void Tick_WorkingLongEnough_ShowsSedentaryReminder()
    {
        // Act: the poll at minute 10 is the 11th.
        var message = RunMinutesUntilMessage(12);

        // Assert
        message.Should().NotBeNull();
        message!.Kind.Should().Be(PetMessageKind.SedentaryReminder);
        message.Actions.Should().Equal(MessageAction.SnoozeReminder, MessageAction.Dismiss);
        message.Duration.Should().Be(TimeSpan.FromMinutes(10));
        _brain.State.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// A user who is away does not get reminded.
    /// </summary>
    [Fact]
    public void Tick_UserAway_NoSedentaryReminder()
    {
        // Arrange
        _idle.IdleTime = TimeSpan.FromMinutes(6);

        // Act
        var message = RunMinutesUntilMessage(60);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// While a pomodoro runs it paces the breaks, so no sedentary reminder appears.
    /// </summary>
    [Fact]
    public void Tick_PomodoroRunning_NoSedentaryReminder()
    {
        // Arrange
        _brain.StartPomodoro();

        // Act: 20 minutes, which is past the sedentary threshold but before the 25-minute work phase ends.
        var message = RunMinutesUntilMessage(20);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// Snoozing makes the reminder return after the short snooze rather than the full interval.
    /// </summary>
    [Fact]
    public void Invoke_SnoozeReminder_RemindsAgainAfterSnooze()
    {
        // Arrange
        RunMinutesUntilMessage(12);
        _brain.Invoke(MessageAction.SnoozeReminder);

        // Act: snooze is 3 minutes.
        _clock.Advance(TimeSpan.FromMinutes(1));
        var early = RunMinutesUntilMessage(2);
        var message = RunMinutesUntilMessage(3);

        // Assert
        _brain.CurrentMessage.Should().BeSameAs(message);
        early.Should().BeNull();
        message.Should().NotBeNull();
        message!.Kind.Should().Be(PetMessageKind.SedentaryReminder);
    }

    /// <summary>
    /// Dismissing without snoozing waits the full interval before the next reminder.
    /// </summary>
    [Fact]
    public void Invoke_DismissReminder_NextReminderAfterFullInterval()
    {
        // Arrange
        RunMinutesUntilMessage(12);
        _brain.Invoke(MessageAction.Dismiss);
        _clock.Advance(TimeSpan.FromMinutes(1));

        // Act: 9 more minutes is not enough, one or two more is.
        var early = RunMinutesUntilMessage(9);
        var message = RunMinutesUntilMessage(3);

        // Assert
        early.Should().BeNull();
        message.Should().NotBeNull();
    }

    /// <summary>
    /// Advances the clock in one-second steps, ticking the brain after each step.
    /// </summary>
    /// <param name="duration">
    /// How long to run.
    /// </param>
    /// <returns>
    /// The state after the last tick.
    /// </returns>
    private PetState RunFor(TimeSpan duration)
    {
        var state = _brain.State;
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += TimeSpan.FromSeconds(1))
        {
            state = _brain.Tick();
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        return state;
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
    /// Load that stays high long enough makes the pet tired.
    /// </summary>
    [Fact]
    public void Tick_SustainedHighLoad_PetBecomesTired()
    {
        // Arrange
        _load.Snapshot = new SystemLoadSnapshot(CpuPercent: 99, MemoryPercent: 50);

        // Act
        var state = RunFor(TimeSpan.FromSeconds(12));

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
        RunFor(TimeSpan.FromSeconds(12));
        _load.Snapshot = new SystemLoadSnapshot(CpuPercent: 5, MemoryPercent: 50);

        // Act
        var state = RunFor(TimeSpan.FromSeconds(12));

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
    /// Running a pomodoro work phase makes the pet focus.
    /// </summary>
    [Fact]
    public void Tick_PomodoroWorking_PetFocuses()
    {
        // Arrange
        _brain.StartPomodoro();

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// A paused pomodoro no longer keeps the pet focused.
    /// </summary>
    [Fact]
    public void Tick_PomodoroPaused_PetStopsFocusing()
    {
        // Arrange
        _brain.StartPomodoro();
        _brain.Tick();
        _brain.PausePomodoro();

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// When work ends the pet raises a message and calls for attention.
    /// </summary>
    [Fact]
    public void Tick_WorkEnds_ShowsBreakMessageAndAlerts()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work);

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Alert);
        _brain.CurrentMessage.Should().NotBeNull();
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.PomodoroWorkCompleted);
        _brain.CurrentMessage.Duration.Should().Be(ShortBreak);
        _brain.CurrentMessage.Count.Should().Be(1);
        _brain.CurrentMessage.Actions.Should().Equal(MessageAction.SkipBreak, MessageAction.Dismiss);
    }

    /// <summary>
    /// When the break ends the pet offers to start the next round.
    /// </summary>
    [Fact]
    public void Tick_BreakEnds_OffersNextRound()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();
        _brain.DismissMessage();
        _clock.Advance(ShortBreak);

        // Act
        _brain.Tick();

        // Assert
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.PomodoroBreakCompleted);
        _brain.CurrentMessage.Actions.Should().Equal(MessageAction.StartPomodoro, MessageAction.Dismiss);
    }

    /// <summary>
    /// A phase that expired while the machine slept produces no reminder.
    /// </summary>
    [Fact]
    public void Tick_WorkExpiredLongAgo_RaisesNoMessage()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work + TimeSpan.FromHours(3));

        // Act
        var state = _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
        state.Should().NotBe(PetState.Alert).And.NotBe(PetState.Focus);
        _brain.Pomodoro.Phase.Should().Be(PomodoroPhase.Stopped);
    }

    /// <summary>
    /// A message fades on its own and the pet settles down again.
    /// </summary>
    [Fact]
    public void Tick_MessageTimesOut_AlertEnds()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();

        // Act
        _clock.Advance(TimeSpan.FromSeconds(31));
        var state = _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
        state.Should().NotBe(PetState.Alert);
    }

    /// <summary>
    /// While the user must not be disturbed the message is held back and the pet stays calm.
    /// </summary>
    [Fact]
    public void Tick_DoNotDisturb_HoldsMessageBack()
    {
        // Arrange
        _fullscreen.DoNotDisturb = true;
        _brain.StartPomodoro();
        _clock.Advance(Work);

        // Act
        var state = _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
        state.Should().NotBe(PetState.Alert);
    }

    /// <summary>
    /// A held-back message appears once the user can be disturbed again.
    /// </summary>
    [Fact]
    public void Tick_DoNotDisturbEnds_ShowsHeldMessage()
    {
        // Arrange
        _fullscreen.DoNotDisturb = true;
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();
        _fullscreen.DoNotDisturb = false;

        // Act
        var state = _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().NotBeNull();
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// Entering do-not-disturb while a message is showing hides it until the user can be disturbed again.
    /// </summary>
    [Fact]
    public void Tick_DoNotDisturbStartsWhileShowing_HidesMessage()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();
        _fullscreen.DoNotDisturb = true;

        // Act
        _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// The skip-break action ends the break and closes the bubble.
    /// </summary>
    [Fact]
    public void Invoke_SkipBreak_StopsPomodoroAndClosesMessage()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();

        // Act
        _brain.Invoke(MessageAction.SkipBreak);

        // Assert
        _brain.Pomodoro.Phase.Should().Be(PomodoroPhase.Stopped);
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// The start action begins a new work phase and closes the bubble.
    /// </summary>
    [Fact]
    public void Invoke_StartPomodoro_BeginsWorkAndClosesMessage()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();
        _brain.DismissMessage();
        _clock.Advance(ShortBreak);
        _brain.Tick();

        // Act
        _brain.Invoke(MessageAction.StartPomodoro);

        // Assert
        _brain.Pomodoro.Phase.Should().Be(PomodoroPhase.Work);
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// Dismiss only closes the bubble.
    /// </summary>
    [Fact]
    public void Invoke_Dismiss_OnlyClosesMessage()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();

        // Act
        _brain.Invoke(MessageAction.Dismiss);

        // Assert
        _brain.CurrentMessage.Should().BeNull();
        _brain.Pomodoro.Phase.Should().Be(PomodoroPhase.ShortBreak);
    }

    /// <summary>
    /// Clicking the pet acknowledges the message on screen.
    /// </summary>
    [Fact]
    public void OnClicked_WithMessageShowing_DismissesIt()
    {
        // Arrange
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();

        // Act
        _brain.OnClicked();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
        _brain.State.Should().Be(PetState.Happy);
    }

    /// <summary>
    /// Showing and closing a message raises <see cref="PetBrain.MessageChanged"/> each time.
    /// </summary>
    [Fact]
    public void MessageChanged_ShownThenDismissed_RaisedTwice()
    {
        // Arrange
        var raised = 0;
        _brain.MessageChanged += (_, _) => raised++;
        _brain.StartPomodoro();
        _clock.Advance(Work);

        // Act
        _brain.Tick();
        _brain.DismissMessage();

        // Assert
        raised.Should().Be(2);
    }

    /// <summary>
    /// Dismissing with nothing on screen raises no event.
    /// </summary>
    [Fact]
    public void DismissMessage_NothingShown_RaisesNothing()
    {
        // Arrange
        var raised = 0;
        _brain.MessageChanged += (_, _) => raised++;

        // Act
        _brain.DismissMessage();

        // Assert
        raised.Should().Be(0);
    }

    /// <summary>
    /// The status event fires when a pomodoro starts and again as whole minutes tick away, but not on every tick.
    /// </summary>
    [Fact]
    public void PomodoroStatusChanged_RunningPhase_FiresOncePerMinute()
    {
        // Arrange
        var raised = 0;
        _brain.PomodoroStatusChanged += (_, _) => raised++;
        _brain.StartPomodoro();

        // Act: 150 seconds of ticks span the start plus two minute boundaries.
        RunFor(TimeSpan.FromSeconds(150));

        // Assert
        raised.Should().Be(3);
    }

    /// <summary>
    /// Pausing changes the visible status.
    /// </summary>
    [Fact]
    public void PomodoroStatusChanged_Paused_Fires()
    {
        // Arrange
        _brain.StartPomodoro();
        _brain.Tick();
        var raised = 0;
        _brain.PomodoroStatusChanged += (_, _) => raised++;

        // Act
        _brain.PausePomodoro();
        _brain.Tick();

        // Assert
        raised.Should().Be(1);
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
    /// Do-not-disturb detector stub with a settable value.
    /// </summary>
    private sealed class FakeFullscreenDetector : IFullscreenDetector
    {
        /// <summary>
        /// Gets or sets whether the user should not be disturbed.
        /// </summary>
        public bool DoNotDisturb { get; set; }

        /// <inheritdoc />
        public bool IsDoNotDisturb()
        {
            return DoNotDisturb;
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
