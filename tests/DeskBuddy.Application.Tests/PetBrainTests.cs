using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Pomodoro;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;
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
    /// How long a full-screen application must be gone before the pet comes back, in the tests.
    /// </summary>
    private static readonly TimeSpan RestoreDelay = TimeSpan.FromSeconds(2);

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
    /// The fake foreground application detector the brain reads.
    /// </summary>
    private readonly FakeForegroundApp _foreground = new();

    /// <summary>
    /// The fake process list the brain reads.
    /// </summary>
    private readonly FakeProcessList _processes = new();

    /// <summary>
    /// The development detection the brain consults.
    /// </summary>
    private readonly DevActivityPolicy _devActivity;

    /// <summary>
    /// The scenario switches the brain follows.
    /// </summary>
    private readonly ScenarioSwitches _switches = new();

    /// <summary>
    /// The scenario switches of the clock; off, as the fake clock starts on New Year Day.
    /// </summary>
    private readonly ScenarioSwitches _clockSwitches = new() { IsEnabled = false };

    /// <summary>
    /// The fake power supply the brain reads.
    /// </summary>
    private readonly FakePowerStatusProvider _power = new();

    /// <summary>
    /// The meeting detection the brain consults.
    /// </summary>
    private readonly MeetingPolicy _meeting = new() { Processes = PetSettings.DefaultMeetingProcesses };

    /// <summary>
    /// The clock shared by every time-dependent component.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The visibility rules the brain updates.
    /// </summary>
    private readonly PetVisibility _visibility;

    /// <summary>
    /// The chatter policy the brain consults.
    /// </summary>
    private readonly ChatterPolicy _chatter;

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
        _visibility = new PetVisibility(_clock, RestoreDelay);
        _chatter = new ChatterPolicy(_clock, new Random(1));
        _devActivity = new DevActivityPolicy(behavior, _clock) { Processes = PetSettings.DefaultDevProcesses };
        var devScenarios = new DevScenarios(
            _foreground,
            _processes,
            _devActivity,
            new BuildActivityPolicy(behavior, _clock),
            new GitSyncPolicy(behavior, _clock),
            new LateNightPolicy(behavior, _clock),
            _switches,
            behavior);
        var systemScenarios = new SystemScenarios(
            _foreground,
            _power,
            _meeting,
            new WelcomeBackPolicy(behavior),
            new PowerPolicy(behavior),
            new ScheduleClock(behavior, _clock, _clockSwitches),
            _switches);
        _brain = new PetBrain(
            _idle,
            _load,
            new LoadReactionPolicy(new SystemLoadOptions(), _clock),
            _fullscreen,
            new PomodoroSession(new PomodoroOptions(), _clock),
            new SedentaryReminderPolicy(sedentary, _clock),
            new PetMessageQueue(behavior.MessageDuration, _clock),
            new PetBehaviorStateMachine(behavior, _clock, new Random(1)),
            _visibility,
            _chatter,
            devScenarios,
            systemScenarios);
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
    /// Fall notifications reach the state machine.
    /// </summary>
    [Fact]
    public void OnFallStartedAndEnded_Forwarded_ToStateMachine()
    {
        // Act & Assert
        _brain.OnFallStarted();
        _brain.State.Should().Be(PetState.Fall);

        _brain.OnFallEnded();
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
    /// A full-screen application hides the pet at once and announces it.
    /// </summary>
    [Fact]
    public void Tick_FullscreenStarts_HidesPetAndRaisesVisibilityChanged()
    {
        // Arrange
        var raised = 0;
        _brain.VisibilityChanged += (_, _) => raised++;
        _fullscreen.Level = DoNotDisturbLevel.Fullscreen;

        // Act
        _brain.Tick();

        // Assert
        _brain.Visibility.IsVisible.Should().BeFalse();
        raised.Should().Be(1);
    }

    /// <summary>
    /// A quiet do-not-disturb (such as Focus Assist) holds messages back but does not hide the pet.
    /// </summary>
    [Fact]
    public void Tick_QuietLevel_KeepsPetVisible()
    {
        // Arrange
        _fullscreen.Level = DoNotDisturbLevel.Quiet;

        // Act
        _brain.Tick();

        // Assert
        _brain.Visibility.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// The pet returns only after the full-screen application has been gone for the restore delay.
    /// </summary>
    [Fact]
    public void Tick_FullscreenEnds_PetReturnsAfterRestoreDelay()
    {
        // Arrange
        _fullscreen.Level = DoNotDisturbLevel.Fullscreen;
        _brain.Tick();
        _fullscreen.Level = DoNotDisturbLevel.None;
        _brain.Tick();

        // Act
        var visibleTooSoon = _brain.Visibility.IsVisible;
        _clock.Advance(RestoreDelay);
        _brain.Tick();

        // Assert
        visibleTooSoon.Should().BeFalse();
        _brain.Visibility.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// A message that comes due while the pet is hidden by the user is shown once the pet is back.
    /// </summary>
    [Fact]
    public void Tick_MessageDueWhilePetHidden_ShownWhenPetReturns()
    {
        // Arrange
        _brain.SetManuallyHidden(true);
        _brain.StartPomodoro();
        _clock.Advance(Work);
        _brain.Tick();
        var shownWhileHidden = _brain.CurrentMessage;

        // Act
        _brain.SetManuallyHidden(false);
        _brain.Tick();

        // Assert
        shownWhileHidden.Should().BeNull();
        _brain.CurrentMessage.Should().NotBeNull();
    }

    /// <summary>
    /// Hiding and showing at the user's request is announced, and toggling twice restores the pet.
    /// </summary>
    [Fact]
    public void ToggleManuallyHidden_Twice_HidesThenShowsAndRaisesEachTime()
    {
        // Arrange
        var raised = 0;
        _brain.VisibilityChanged += (_, _) => raised++;

        // Act
        _brain.ToggleManuallyHidden();
        var hidden = _brain.Visibility.IsVisible;
        _brain.ToggleManuallyHidden();

        // Assert
        hidden.Should().BeFalse();
        _brain.Visibility.IsVisible.Should().BeTrue();
        raised.Should().Be(2);
    }

    /// <summary>
    /// Hiding a pet that is already hidden does not announce anything.
    /// </summary>
    [Fact]
    public void SetManuallyHidden_AlreadyHidden_DoesNotRaise()
    {
        // Arrange
        _brain.SetManuallyHidden(true);
        var raised = 0;
        _brain.VisibilityChanged += (_, _) => raised++;

        // Act
        _brain.SetManuallyHidden(true);

        // Assert
        raised.Should().Be(0);
    }

    /// <summary>
    /// The pomodoro keeps running while the pet is hidden.
    /// </summary>
    [Fact]
    public void Tick_PetHidden_PomodoroKeepsRunning()
    {
        // Arrange
        _brain.StartPomodoro();
        _brain.SetManuallyHidden(true);

        // Act
        _clock.Advance(Work);
        _brain.Tick();

        // Assert
        _brain.Pomodoro.Phase.Should().Be(PomodoroPhase.ShortBreak);
    }

    /// <summary>
    /// Runs the brain once a minute, dismissing reminders on the way, until a casual remark appears or the time is up.
    /// </summary>
    /// <param name="minutes">
    /// How many minutes to run at most.
    /// </param>
    /// <param name="chatterEnabled">
    /// Whether the chatter policy is switched on.
    /// </param>
    /// <param name="idle">
    /// How long the user has been away from keyboard and mouse during the run.
    /// </param>
    /// <returns>
    /// The first casual remark that appeared, if any.
    /// </returns>
    private PetMessage? RunUntilChatter(int minutes, bool chatterEnabled = true, TimeSpan idle = default)
    {
        _chatter.IsEnabled = chatterEnabled;
        for (var minute = 0; minute < minutes; minute++)
        {
            _idle.IdleTime = idle;
            _brain.Tick();
            if (_brain.CurrentMessage is { Kind: PetMessageKind.Chatter } remark)
            {
                return remark;
            }

            _brain.DismissMessage();
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        return null;
    }

    /// <summary>
    /// A roaming pet says something now and then, and it is shown briefly without raising the alert.
    /// </summary>
    [Fact]
    public void Tick_ChatterEnabled_ShowsShortRemarkWithoutAlert()
    {
        // Act
        var message = RunUntilChatter(20);

        // Assert
        message.Should().NotBeNull();
        message!.Kind.Should().Be(PetMessageKind.Chatter);
        message.Actions.Should().BeEmpty();
        message.Lifetime.Should().Be(TimeSpan.FromSeconds(8));
        _brain.State.Should().NotBe(PetState.Alert);
    }

    /// <summary>
    /// A casual remark fades on its own after its short lifetime.
    /// </summary>
    [Fact]
    public void Tick_ChatterShown_DisappearsAfterLifetime()
    {
        // Arrange
        RunUntilChatter(20);

        // Act
        _clock.Advance(TimeSpan.FromSeconds(9));
        _idle.IdleTime = TimeSpan.Zero;
        _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// The pet stays quiet while the chatter policy is off, however long it waits.
    /// </summary>
    [Fact]
    public void Tick_ChatterDisabled_NeverSpeaks()
    {
        // Act
        var message = RunUntilChatter(60, chatterEnabled: false);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// A running pomodoro work phase silences the pet.
    /// </summary>
    [Fact]
    public void Tick_PomodoroWorkRunning_NoChatter()
    {
        // Arrange
        _brain.StartPomodoro();

        // Act
        var message = RunUntilChatter(20);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// A user who is away is not spoken to.
    /// </summary>
    [Fact]
    public void Tick_UserAway_NoChatter()
    {
        // Act
        var message = RunUntilChatter(60, idle: TimeSpan.FromMinutes(2));

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// Reaching the pointer makes the pet happy.
    /// </summary>
    [Fact]
    public void OnReachedPointer_Idle_ThenHappy()
    {
        // Act
        _brain.OnReachedPointer();

        // Assert
        _brain.State.Should().Be(PetState.Happy);
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
    /// A development tool in front while the user is active makes the pet sit down to code.
    /// </summary>
    [Fact]
    public void Tick_DevelopmentToolInFront_PetCodes()
    {
        // Arrange
        _foreground.ProcessName = "Code";

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// An application that is not a development tool does not make the pet code.
    /// </summary>
    [Fact]
    public void Tick_OtherApplicationInFront_PetDoesNotCode()
    {
        // Arrange
        _foreground.ProcessName = "chrome";

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().NotBe(PetState.Coding);
    }

    /// <summary>
    /// With development detection and the meeting scenario both off the pet never codes and the foreground window is
    /// not even looked at.
    /// </summary>
    [Fact]
    public void Tick_DevelopmentDetectionOff_DoesNotLookAtForegroundWindow()
    {
        // Arrange
        _devActivity.IsEnabled = false;
        _switches.Disabled = [nameof(PetState.Meeting)];
        _foreground.ProcessName = "Code";

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().NotBe(PetState.Coding);
        _foreground.QueryCount.Should().Be(0);
    }

    /// <summary>
    /// A meeting application in front makes the pet keep company in a headset.
    /// </summary>
    [Fact]
    public void Tick_MeetingApplicationInFront_PetIsInMeeting()
    {
        // Arrange
        _foreground.ProcessName = "Zoom";

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Meeting);
    }

    /// <summary>
    /// The pet keeps quiet during a meeting, however long it waits.
    /// </summary>
    [Fact]
    public void Tick_Meeting_NoChatter()
    {
        // Arrange
        _foreground.ProcessName = "Zoom";

        // Act
        var message = RunUntilChatter(60);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// A meeting outranks a running build: the pet keeps still and quiet rather than showing the loading ring.
    /// </summary>
    [Fact]
    public void Tick_MeetingWhileBuilding_MeetingWins()
    {
        // Arrange
        _foreground.ProcessName = "Zoom";
        _processes.Names = new HashSet<string>(["msbuild"], StringComparer.OrdinalIgnoreCase);
        _load.Snapshot = new SystemLoadSnapshot(95, 40);
        PetState state = default;

        // Act
        for (var second = 0; second < 8; second++)
        {
            state = _brain.Tick();
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        // Assert
        state.Should().Be(PetState.Meeting);
    }

    /// <summary>
    /// The user coming back after a long absence is welcomed: the pet wakes up waving and says so.
    /// </summary>
    [Fact]
    public void Tick_BackAfterLongAbsence_PetWelcomesTheUser()
    {
        // Arrange
        _idle.IdleTime = TimeSpan.FromMinutes(45);
        _brain.Tick().Should().Be(PetState.Sleep);
        _clock.Advance(TimeSpan.FromSeconds(1));
        _idle.IdleTime = TimeSpan.FromSeconds(1);

        // Act
        var state = _brain.Tick();
        _clock.Advance(TimeSpan.FromSeconds(1));
        _brain.Tick();

        // Assert
        state.Should().Be(PetState.Welcome);
        _brain.CurrentMessage.Should().NotBeNull();
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.Remark);
        _brain.CurrentMessage.Count.Should().Be((int)PetState.Welcome);
    }

    /// <summary>
    /// A low battery makes the pet show an empty battery, and plugging in makes it charge up.
    /// </summary>
    [Fact]
    public void Tick_BatteryLowThenPluggedIn_PetShowsLowBatteryThenCharges()
    {
        // Arrange
        _power.Status = new PowerStatus(true, 10, false);
        var low = _brain.Tick();
        _clock.Advance(TimeSpan.FromSeconds(1));
        _power.Status = new PowerStatus(true, 10, true);

        // Act
        var charging = _brain.Tick();

        // Assert
        low.Should().Be(PetState.LowBattery);
        charging.Should().Be(PetState.Charging);
    }

    /// <summary>
    /// A state the pet is made to act out is shown for its duration.
    /// </summary>
    [Fact]
    public void ActOut_State_PetShowsItThenCarriesOn()
    {
        // Arrange
        _brain.ActOut(PetState.Focus, TimeSpan.FromSeconds(3));

        // Act
        var during = _brain.Tick();
        _clock.Advance(TimeSpan.FromSeconds(3));
        var after = _brain.Tick();

        // Assert
        during.Should().Be(PetState.Focus);
        after.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A remark appears in the speech bubble, and unlike a reminder it does not make the pet call for attention.
    /// </summary>
    [Fact]
    public void Say_PetOnScreen_ShowsRemarkWithoutAlerting()
    {
        // Arrange
        _brain.Tick();

        // Act
        var queued = _brain.Say(PetState.Coding);
        var state = _brain.Tick();

        // Assert
        queued.Should().BeTrue();
        _brain.CurrentMessage.Should().NotBeNull();
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.Remark);
        _brain.CurrentMessage.Count.Should().Be((int)PetState.Coding);
        state.Should().NotBe(PetState.Alert);
    }

    /// <summary>
    /// A remark is dropped, not saved for later, while the user must not be disturbed.
    /// </summary>
    [Fact]
    public void Say_DoNotDisturb_IsDropped()
    {
        // Arrange
        _fullscreen.DoNotDisturb = true;
        _brain.Tick();

        // Act
        var queued = _brain.Say(PetState.Coding);
        _fullscreen.DoNotDisturb = false;
        _brain.Tick();

        // Assert
        queued.Should().BeFalse();
        _brain.CurrentMessage.Should().BeNull();
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
        /// Gets or sets the level to report.
        /// </summary>
        public DoNotDisturbLevel Level { get; set; }

        /// <summary>
        /// Sets a quiet do-not-disturb (messages held back, pet stays) or none.
        /// </summary>
        public bool DoNotDisturb
        {
            set => Level = value ? DoNotDisturbLevel.Quiet : DoNotDisturbLevel.None;
        }

        /// <inheritdoc />
        public DoNotDisturbLevel GetLevel()
        {
            return Level;
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

    /// <summary>
    /// At lunch time the pet acts out a meal and says the remark of lunch, not of dinner.
    /// </summary>
    [Fact]
    public void Tick_LunchTime_ActsOutMealWithLunchRemark()
    {
        // Arrange
        UseNoonOnAWeekday();

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Meal);
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.Remark);
        _brain.CurrentMessage.Count.Should().Be(RemarkCode.Encode(PetState.Meal, ScheduleClock.Lunch));
    }

    /// <summary>
    /// A pet that is being dragged does not use up the occasion; it comes once the pet is put down.
    /// </summary>
    [Fact]
    public void Tick_LunchTimeWhileDragging_MealComesAfterTheDrop()
    {
        // Arrange
        UseNoonOnAWeekday();
        _brain.OnDragStarted();

        // Act
        var whileDragging = _brain.Tick();
        _brain.OnDragEnded();
        var afterDrop = _brain.Tick();

        // Assert
        whileDragging.Should().Be(PetState.Drag);
        afterDrop.Should().Be(PetState.Meal);
    }

    /// <summary>
    /// A pet that is falling does not use up the occasion either; it comes once the pet has landed.
    /// </summary>
    [Fact]
    public void Tick_LunchTimeWhileFalling_MealComesAfterTheLanding()
    {
        // Arrange
        UseNoonOnAWeekday();
        _brain.OnFallStarted();

        // Act
        var whileFalling = _brain.Tick();
        _brain.OnFallEnded();
        var afterLanding = _brain.Tick();

        // Assert
        whileFalling.Should().Be(PetState.Fall);
        afterLanding.Should().Be(PetState.Meal);
    }

    /// <summary>
    /// Sets the clock to a Wednesday noon in UTC and switches the clock scenarios on.
    /// </summary>
    private void UseNoonOnAWeekday()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _clock.SetUtcNow(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));
        _clockSwitches.IsEnabled = true;
        _clockSwitches.Disabled = [nameof(PetState.Chime)];
    }
}
