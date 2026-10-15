using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="PetBrain"/>.
/// </summary>
public partial class PetBrainTests
{
    /// <summary>
    /// Sleep threshold used by the test options.
    /// </summary>
    private static readonly TimeSpan SleepAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Long enough for the sedentary reminder to come due after a first tick.
    /// </summary>
    private static readonly TimeSpan ReminderDelay = TimeSpan.FromMinutes(25);

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
    /// The chat detection, knowing the default chat applications.
    /// </summary>
    private readonly ChatPolicy _chat = new() { Processes = PetSettings.DefaultChatProcesses };

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
            _chat,
            new WelcomeBackPolicy(behavior),
            new PowerPolicy(behavior),
            new ScheduleClock(behavior, _clock, _clockSwitches),
            _switches);
        _brain = new PetBrain(
            _idle,
            _load,
            new LoadReactionPolicy(new SystemLoadOptions(), _clock),
            _fullscreen,
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
