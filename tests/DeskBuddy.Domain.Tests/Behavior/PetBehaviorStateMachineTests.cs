namespace DeskBuddy.Domain.Tests.Behavior;

/// <summary>
/// Tests for <see cref="PetBehaviorStateMachine"/>. Option ranges use equal min/max so timings are deterministic.
/// </summary>
public partial class PetBehaviorStateMachineTests
{
    /// <summary>
    /// Fixed idle dwell used by the test options.
    /// </summary>
    private static readonly TimeSpan IdleDwell = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Fixed walk duration used by the test options.
    /// </summary>
    private static readonly TimeSpan WalkDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Fixed happy duration used by the test options.
    /// </summary>
    private static readonly TimeSpan HappyDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Idle time after which the pet sleeps in the test options.
    /// </summary>
    private static readonly TimeSpan SleepAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Inputs for an active user on a healthy system.
    /// </summary>
    private static readonly PetInputs Active = new(TimeSpan.Zero);

    /// <summary>
    /// Inputs for an active user on a system under heavy load.
    /// </summary>
    private static readonly PetInputs Strained = new(TimeSpan.Zero, IsSystemStrained: true);

    /// <summary>
    /// Inputs for an active user while a message is on screen.
    /// </summary>
    private static readonly PetInputs Alerting = new(TimeSpan.Zero, HasAlert: true);

    /// <summary>
    /// Inputs for an active user while working in a development tool.
    /// </summary>
    private static readonly PetInputs Developing = new(TimeSpan.Zero, IsDeveloping: true);

    /// <summary>
    /// The fake clock shared by the machine under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The machine under test.
    /// </summary>
    private readonly PetBehaviorStateMachine _machine;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetBehaviorStateMachineTests"/> class.
    /// </summary>
    public PetBehaviorStateMachineTests()
    {
        var options = new PetBehaviorOptions
        {
            SleepAfter = SleepAfter,
            IdleDwellMin = IdleDwell,
            IdleDwellMax = IdleDwell,
            WalkDurationMin = WalkDuration,
            WalkDurationMax = WalkDuration,
            HappyDuration = HappyDuration,
        };
        _machine = new PetBehaviorStateMachine(options, _clock, new Random(42));
    }

    /// <summary>
    /// Makes a machine that is walking and is to hop after a fixed time.
    /// </summary>
    /// <param name="hopAfter">
    /// How long into the walk the pet hops.
    /// </param>
    /// <param name="hopDuration">
    /// How long the hop lasts.
    /// </param>
    /// <returns>
    /// The machine, in the first moment of a walk.
    /// </returns>
    private PetBehaviorStateMachine CreateHoppingMachine(TimeSpan hopAfter, TimeSpan hopDuration)
    {
        var options = new PetBehaviorOptions
        {
            SleepAfter = SleepAfter,
            IdleDwellMin = IdleDwell,
            IdleDwellMax = IdleDwell,
            WalkDurationMin = WalkDuration,
            WalkDurationMax = WalkDuration,
            HopIntervalMin = hopAfter,
            HopIntervalMax = hopAfter,
        };
        var machine = new PetBehaviorStateMachine(options, _clock, new Random(42)) { HopDuration = hopDuration };
        _clock.Advance(IdleDwell);
        machine.Update(Active);
        return machine;
    }

    /// <summary>
    /// A new machine starts in Idle.
    /// </summary>
    [Fact]
    public void Constructor_Default_StartsIdle()
    {
        // Assert
        _machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Before the idle dwell elapses the pet keeps standing.
    /// </summary>
    [Fact]
    public void Update_BeforeDwellElapsed_StaysIdle()
    {
        // Arrange
        _clock.Advance(IdleDwell - TimeSpan.FromMilliseconds(1));

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Once the idle dwell elapses the pet starts walking.
    /// </summary>
    [Fact]
    public void Update_DwellElapsed_StartsWalking()
    {
        // Arrange
        _clock.Advance(IdleDwell);

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Walk);
        _machine.WalkDirection.Should().BeOneOf(-1, 1);
    }

    /// <summary>
    /// A walk ends after its duration and the pet goes back to standing.
    /// </summary>
    [Fact]
    public void Update_WalkDurationElapsed_ReturnsToIdle()
    {
        // Arrange
        _clock.Advance(IdleDwell);
        _machine.Update(Active);
        _clock.Advance(WalkDuration);

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Long user inactivity puts the pet to sleep.
    /// </summary>
    [Fact]
    public void Update_UserIdleReachesThreshold_Sleeps()
    {
        // Act
        var state = _machine.Update(new PetInputs(SleepAfter));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// Sleeping takes precedence over walking.
    /// </summary>
    [Fact]
    public void Update_WalkingAndUserGoesAway_Sleeps()
    {
        // Arrange
        _clock.Advance(IdleDwell);
        _machine.Update(Active);

        // Act
        var state = _machine.Update(new PetInputs(SleepAfter));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// The pet stays asleep, however much time passes, while the user remains away.
    /// </summary>
    [Fact]
    public void Update_SleepingAndUserStillAway_StaysAsleep()
    {
        // Arrange
        _machine.Update(new PetInputs(SleepAfter));
        _clock.Advance(TimeSpan.FromHours(1));

        // Act
        var state = _machine.Update(new PetInputs(SleepAfter + TimeSpan.FromHours(1)));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// When the user returns the pet wakes up and stands before it may walk again.
    /// </summary>
    [Fact]
    public void Update_SleepingAndUserReturns_WakesToIdle()
    {
        // Arrange
        _machine.Update(new PetInputs(SleepAfter));
        _clock.Advance(TimeSpan.FromHours(1));

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Both walk directions occur across many walks.
    /// </summary>
    [Fact]
    public void WalkDirection_ManyWalks_UsesBothDirections()
    {
        // Arrange
        var directions = new HashSet<int>();

        // Act
        for (var i = 0; i < 40; i++)
        {
            _clock.Advance(IdleDwell);
            _machine.Update(Active);
            directions.Add(_machine.WalkDirection);
            _clock.Advance(WalkDuration);
            _machine.Update(Active);
        }

        // Assert
        directions.Should().BeEquivalentTo([-1, 1]);
    }
}
