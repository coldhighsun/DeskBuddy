using AwesomeAssertions;
using DeskBuddy.Domain.Behavior;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Behavior;

/// <summary>
/// Tests for <see cref="PetBehaviorStateMachine"/>. Option ranges use equal min/max so timings are deterministic.
/// </summary>
public class PetBehaviorStateMachineTests
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
    /// Inputs for an active user during a pomodoro work phase.
    /// </summary>
    private static readonly PetInputs Focusing = new(TimeSpan.Zero, IsFocusing: true);

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
    /// Dragging overrides the current state.
    /// </summary>
    [Fact]
    public void OnDragStarted_AnyState_EntersDrag()
    {
        // Act
        _machine.OnDragStarted();

        // Assert
        _machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// While dragged the pet neither walks nor sleeps.
    /// </summary>
    [Fact]
    public void Update_WhileDragging_StaysDrag()
    {
        // Arrange
        _machine.OnDragStarted();
        _clock.Advance(TimeSpan.FromHours(1));

        // Act
        var state = _machine.Update(new PetInputs(SleepAfter));

        // Assert
        state.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// After being dropped the pet stands, and the dwell restarts rather than walking away immediately.
    /// </summary>
    [Fact]
    public void OnDragEnded_AfterLongDrag_IdlesAndRestartsDwell()
    {
        // Arrange
        _machine.OnDragStarted();
        _clock.Advance(TimeSpan.FromMinutes(1));
        _machine.OnDragEnded();

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A pet dropped from a height falls, and keeps falling however long it takes.
    /// </summary>
    [Fact]
    public void Update_WhileFalling_StaysFall()
    {
        // Arrange
        _machine.OnFallStarted();
        _clock.Advance(TimeSpan.FromHours(1));

        // Act
        var state = _machine.Update(new PetInputs(SleepAfter));

        // Assert
        state.Should().Be(PetState.Fall);
    }

    /// <summary>
    /// Landing ends the fall and the pet stands again.
    /// </summary>
    [Fact]
    public void OnFallEnded_WhileFalling_EntersIdle()
    {
        // Arrange
        _machine.OnFallStarted();

        // Act
        _machine.OnFallEnded();

        // Assert
        _machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Landing when the pet is not falling does nothing.
    /// </summary>
    [Fact]
    public void OnFallEnded_NotFalling_DoesNotChangeState()
    {
        // Arrange
        _machine.OnDragStarted();

        // Act
        _machine.OnFallEnded();

        // Assert
        _machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// Grabbing the pet in mid-air interrupts the fall.
    /// </summary>
    [Fact]
    public void OnDragStarted_WhileFalling_EntersDrag()
    {
        // Arrange
        _machine.OnFallStarted();

        // Act
        _machine.OnDragStarted();

        // Assert
        _machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// Clicks and short scenes do not interrupt a fall.
    /// </summary>
    [Fact]
    public void OnMoment_WhileFalling_IsIgnored()
    {
        // Arrange
        _machine.OnFallStarted();

        // Act
        _machine.OnMoment(PetState.Dizzy, TimeSpan.FromSeconds(2));
        _machine.OnClicked();

        // Assert
        _machine.State.Should().Be(PetState.Fall);
    }

    /// <summary>
    /// Ending a drag that never started does nothing.
    /// </summary>
    [Fact]
    public void OnDragEnded_NotDragging_DoesNotChangeState()
    {
        // Arrange
        _clock.Advance(IdleDwell);
        _machine.Update(Active);

        // Act
        _machine.OnDragEnded();

        // Assert
        _machine.State.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// A click makes the pet happy.
    /// </summary>
    [Fact]
    public void OnClicked_Idle_EntersHappy()
    {
        // Act
        _machine.OnClicked();

        // Assert
        _machine.State.Should().Be(PetState.Happy);
    }

    /// <summary>
    /// Something pleasing the pet, such as reaching the pointer it chased, makes it happy.
    /// </summary>
    [Fact]
    public void OnDelighted_Walking_EntersHappy()
    {
        // Arrange
        _clock.Advance(TimeSpan.FromSeconds(30));
        _machine.Update(new PetInputs(TimeSpan.Zero));

        // Act
        _machine.OnDelighted();

        // Assert
        _machine.State.Should().Be(PetState.Happy);
    }

    /// <summary>
    /// Being delighted while dragged is ignored.
    /// </summary>
    [Fact]
    public void OnDelighted_Dragging_IsIgnored()
    {
        // Arrange
        _machine.OnDragStarted();

        // Act
        _machine.OnDelighted();

        // Assert
        _machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// A click wakes a sleeping pet.
    /// </summary>
    [Fact]
    public void OnClicked_Sleeping_EntersHappy()
    {
        // Arrange
        _machine.Update(new PetInputs(SleepAfter));

        // Act
        _machine.OnClicked();

        // Assert
        _machine.State.Should().Be(PetState.Happy);
    }

    /// <summary>
    /// A click during a drag is ignored.
    /// </summary>
    [Fact]
    public void OnClicked_Dragging_IsIgnored()
    {
        // Arrange
        _machine.OnDragStarted();

        // Act
        _machine.OnClicked();

        // Assert
        _machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// Happiness is short-lived: the pet returns to standing after the happy duration.
    /// </summary>
    [Fact]
    public void Update_HappyDurationElapsed_ReturnsToIdle()
    {
        // Arrange
        _machine.OnClicked();
        _clock.Advance(HappyDuration);

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// The pet stays happy until the happy duration elapses.
    /// </summary>
    [Fact]
    public void Update_BeforeHappyDurationElapsed_StaysHappy()
    {
        // Arrange
        _machine.OnClicked();
        _clock.Advance(HappyDuration - TimeSpan.FromMilliseconds(1));

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Happy);
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

    /// <summary>
    /// A strained system makes an idle pet tired right away, without waiting for the dwell.
    /// </summary>
    [Fact]
    public void Update_SystemStrained_BecomesTired()
    {
        // Act
        var state = _machine.Update(Strained);

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// A strained system interrupts a walk.
    /// </summary>
    [Fact]
    public void Update_WalkingAndSystemStrained_BecomesTired()
    {
        // Arrange
        _clock.Advance(IdleDwell);
        _machine.Update(Active);

        // Act
        var state = _machine.Update(Strained);

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// A tired pet does not wander off, however much time passes.
    /// </summary>
    [Fact]
    public void Update_TiredForLongTime_StaysTired()
    {
        // Arrange
        _machine.Update(Strained);
        _clock.Advance(TimeSpan.FromHours(1));

        // Act
        var state = _machine.Update(Strained);

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// When the system recovers the pet stands again, and the dwell restarts rather than walking away immediately.
    /// </summary>
    [Fact]
    public void Update_SystemRecovers_ReturnsToIdleAndRestartsDwell()
    {
        // Arrange
        _machine.Update(Strained);
        _clock.Advance(TimeSpan.FromMinutes(10));

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Falling asleep outranks being tired.
    /// </summary>
    [Fact]
    public void Update_UserAwayAndSystemStrained_Sleeps()
    {
        // Act
        var state = _machine.Update(new PetInputs(SleepAfter, IsSystemStrained: true));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// A pet that wakes up while the system is still strained is tired on the next update.
    /// </summary>
    [Fact]
    public void Update_WakesWhileSystemStrained_ThenBecomesTired()
    {
        // Arrange
        _machine.Update(new PetInputs(SleepAfter, IsSystemStrained: true));
        _machine.Update(Strained);

        // Act
        var state = _machine.Update(Strained);

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// Being dragged outranks being tired.
    /// </summary>
    [Fact]
    public void Update_DraggingAndSystemStrained_StaysDrag()
    {
        // Arrange
        _machine.OnDragStarted();

        // Act
        var state = _machine.Update(Strained);

        // Assert
        state.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// A click cheers up a tired pet for the happy duration.
    /// </summary>
    [Fact]
    public void OnClicked_Tired_EntersHappy()
    {
        // Arrange
        _machine.Update(Strained);

        // Act
        _machine.OnClicked();

        // Assert
        _machine.State.Should().Be(PetState.Happy);
        _machine.Update(Strained).Should().Be(PetState.Happy);
    }

    /// <summary>
    /// A pending message makes the pet call for attention immediately.
    /// </summary>
    [Fact]
    public void Update_HasAlert_EntersAlert()
    {
        // Act
        var state = _machine.Update(Alerting);

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// An alert interrupts a walk.
    /// </summary>
    [Fact]
    public void Update_WalkingAndAlert_EntersAlert()
    {
        // Arrange
        _clock.Advance(IdleDwell);
        _machine.Update(Active);

        // Act
        var state = _machine.Update(Alerting);

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// An alert outranks sleep, tiredness and focus.
    /// </summary>
    [Fact]
    public void Update_AlertWithOtherConditions_AlertWins()
    {
        // Act
        var state = _machine.Update(new PetInputs(SleepAfter, IsSystemStrained: true, IsFocusing: true, HasAlert: true));

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// Being dragged outranks an alert, and the alert returns once the pet is dropped.
    /// </summary>
    [Fact]
    public void Update_DraggingThenDroppedWhileAlerting_ReturnsToAlert()
    {
        // Arrange
        _machine.OnDragStarted();

        // Act & Assert
        _machine.Update(Alerting).Should().Be(PetState.Drag);
        _machine.OnDragEnded();
        _machine.Update(Alerting).Should().Be(PetState.Alert);
    }

    /// <summary>
    /// When the message goes away the pet settles back to standing.
    /// </summary>
    [Fact]
    public void Update_AlertEnds_ReturnsToIdle()
    {
        // Arrange
        _machine.Update(Alerting);

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// If the user is still away when the alert ends, the pet goes straight back to sleep.
    /// </summary>
    [Fact]
    public void Update_AlertEndsWhileUserAway_Sleeps()
    {
        // Arrange
        _machine.Update(Alerting);

        // Act
        var state = _machine.Update(new PetInputs(SleepAfter));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// A pomodoro work phase makes the pet focus.
    /// </summary>
    [Fact]
    public void Update_Focusing_EntersFocus()
    {
        // Act
        var state = _machine.Update(Focusing);

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// A focused pet does not wander off, however much time passes.
    /// </summary>
    [Fact]
    public void Update_FocusingForLongTime_NeverWalks()
    {
        // Arrange
        _machine.Update(Focusing);
        _clock.Advance(TimeSpan.FromHours(1));

        // Act
        var state = _machine.Update(Focusing);

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// Focus interrupts a walk already under way.
    /// </summary>
    [Fact]
    public void Update_WalkingThenFocusing_EntersFocus()
    {
        // Arrange
        _clock.Advance(IdleDwell);
        _machine.Update(Active);

        // Act
        var state = _machine.Update(Focusing);

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// When the work phase ends the pet stands and restarts its dwell.
    /// </summary>
    [Fact]
    public void Update_FocusEnds_ReturnsToIdle()
    {
        // Arrange
        _machine.Update(Focusing);
        _clock.Advance(TimeSpan.FromMinutes(25));

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A strained system outranks focus.
    /// </summary>
    [Fact]
    public void Update_FocusingAndSystemStrained_BecomesTired()
    {
        // Act
        var state = _machine.Update(new PetInputs(TimeSpan.Zero, IsSystemStrained: true, IsFocusing: true));

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// Sleep outranks focus: a user who walks away mid-pomodoro sees the pet asleep.
    /// </summary>
    [Fact]
    public void Update_FocusingAndUserAway_Sleeps()
    {
        // Act
        var state = _machine.Update(new PetInputs(SleepAfter, IsFocusing: true));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// A click cheers up a focused pet briefly, after which it focuses again.
    /// </summary>
    [Fact]
    public void OnClicked_Focusing_HappyThenFocusAgain()
    {
        // Arrange
        _machine.Update(Focusing);
        _machine.OnClicked();
        _clock.Advance(HappyDuration);

        // Act
        var state = _machine.Update(Focusing);

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// After the happy moment passes, a still-strained system makes the pet tired again.
    /// </summary>
    [Fact]
    public void Update_HappyEndsWhileSystemStrained_BecomesTiredAgain()
    {
        // Arrange
        _machine.OnClicked();
        _clock.Advance(HappyDuration);

        // Act
        var state = _machine.Update(Strained);

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// A user working in a development tool makes the pet sit down to code.
    /// </summary>
    [Fact]
    public void Update_Developing_EntersCoding()
    {
        // Act
        var state = _machine.Update(Developing);

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// A pet at its computer stays there instead of wandering off.
    /// </summary>
    [Fact]
    public void Update_DevelopingForLongTime_NeverWalks()
    {
        // Arrange
        _machine.Update(Developing);

        // Act
        _clock.Advance(IdleDwell + WalkDuration + IdleDwell);
        var state = _machine.Update(Developing);

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// Coding interrupts a walk already under way.
    /// </summary>
    [Fact]
    public void Update_WalkingThenDeveloping_EntersCoding()
    {
        // Arrange
        _clock.Advance(IdleDwell);
        _machine.Update(Active).Should().Be(PetState.Walk);

        // Act
        var state = _machine.Update(Developing);

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// When the user stops developing the pet gets up and returns to Idle.
    /// </summary>
    [Fact]
    public void Update_DevelopingEnds_ReturnsToIdle()
    {
        // Arrange
        _machine.Update(Developing);

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A user who walks away falls asleep even in front of a development tool.
    /// </summary>
    [Fact]
    public void Update_DevelopingAndUserAway_Sleeps()
    {
        // Act
        var state = _machine.Update(new PetInputs(SleepAfter, IsDeveloping: true));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// A heavily loaded system (a build, say) makes a coding pet tired.
    /// </summary>
    [Fact]
    public void Update_DevelopingAndSystemStrained_BecomesTired()
    {
        // Act
        var state = _machine.Update(new PetInputs(TimeSpan.Zero, IsSystemStrained: true, IsDeveloping: true));

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// A pomodoro work phase, which the user asked for, outranks coding.
    /// </summary>
    [Fact]
    public void Update_DevelopingAndFocusing_Focuses()
    {
        // Act
        var state = _machine.Update(new PetInputs(TimeSpan.Zero, IsFocusing: true, IsDeveloping: true));

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// A message on screen calls for attention even while the user is developing.
    /// </summary>
    [Fact]
    public void Update_DevelopingAndAlert_Alerts()
    {
        // Act
        var state = _machine.Update(new PetInputs(TimeSpan.Zero, HasAlert: true, IsDeveloping: true));

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// A click cheers up a coding pet briefly, after which it codes again.
    /// </summary>
    [Fact]
    public void OnClicked_Developing_HappyThenCodingAgain()
    {
        // Arrange
        _machine.Update(Developing);
        _machine.OnClicked();
        _machine.State.Should().Be(PetState.Happy);
        _clock.Advance(HappyDuration);

        // Act
        var state = _machine.Update(Developing);

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// A moment is acted out right away.
    /// </summary>
    [Fact]
    public void OnMoment_Called_EntersTheState()
    {
        // Act
        _machine.OnMoment(PetState.Alert, TimeSpan.FromSeconds(3));

        // Assert
        _machine.State.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// A moment lasts for its duration and the pet then returns to Idle.
    /// </summary>
    [Fact]
    public void OnMoment_DurationPasses_ReturnsToIdle()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(3);
        _machine.OnMoment(PetState.Focus, duration);

        // Act
        _clock.Advance(duration - TimeSpan.FromSeconds(1));
        var during = _machine.Update(Active);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var after = _machine.Update(Active);

        // Assert
        during.Should().Be(PetState.Focus);
        after.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A moment holds even while the pet would otherwise be Tired, Focused or Coding.
    /// </summary>
    [Fact]
    public void OnMoment_WhileLowerRankedInputsApply_StaysUntilItEnds()
    {
        // Arrange
        _machine.OnMoment(PetState.Happy, TimeSpan.FromSeconds(3));

        // Act
        var state = _machine.Update(new PetInputs(TimeSpan.Zero, IsSystemStrained: true, IsFocusing: true, IsDeveloping: true));

        // Assert
        state.Should().Be(PetState.Happy);
    }

    /// <summary>
    /// When a moment ends, what the pet was held back from takes over.
    /// </summary>
    [Fact]
    public void OnMoment_Ends_LowerRankedStateTakesOver()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(3);
        _machine.OnMoment(PetState.Happy, duration);
        _machine.Update(Developing);

        // Act
        _clock.Advance(duration);
        var state = _machine.Update(Developing);

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// A moment is ignored while the pet is being dragged.
    /// </summary>
    [Fact]
    public void OnMoment_WhileDragging_IsIgnored()
    {
        // Arrange
        _machine.OnDragStarted();

        // Act
        _machine.OnMoment(PetState.Focus, TimeSpan.FromSeconds(3));

        // Assert
        _machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// A user who is away still makes the pet sleep, moment or not.
    /// </summary>
    [Fact]
    public void OnMoment_UserAway_Sleeps()
    {
        // Arrange
        _machine.OnMoment(PetState.Focus, TimeSpan.FromSeconds(30));

        // Act
        var state = _machine.Update(new PetInputs(SleepAfter));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// A message on screen takes over from a moment.
    /// </summary>
    [Fact]
    public void OnMoment_AlertArrives_Alerts()
    {
        // Arrange
        _machine.OnMoment(PetState.Focus, TimeSpan.FromSeconds(30));

        // Act
        var state = _machine.Update(Alerting);

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// A moment that follows a sleep (the user coming back) is not undone by the wake-up.
    /// </summary>
    [Fact]
    public void OnMoment_WhileSleeping_SurvivesWakingUp()
    {
        // Arrange
        _machine.Update(new PetInputs(SleepAfter));
        _machine.OnMoment(PetState.Focus, TimeSpan.FromSeconds(3));

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// An ambient state is held for as long as it is set.
    /// </summary>
    [Fact]
    public void Update_Ambient_EntersAndHoldsIt()
    {
        // Arrange
        var inputs = new PetInputs(TimeSpan.Zero, Ambient: PetState.Focus);

        // Act
        _machine.Update(inputs);
        _clock.Advance(IdleDwell + WalkDuration + IdleDwell);
        var state = _machine.Update(inputs);

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// When the ambient state goes away the pet returns to Idle.
    /// </summary>
    [Fact]
    public void Update_AmbientEnds_ReturnsToIdle()
    {
        // Arrange
        _machine.Update(new PetInputs(TimeSpan.Zero, Ambient: PetState.Focus));

        // Act
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A change from one ambient state to another is followed at once.
    /// </summary>
    [Fact]
    public void Update_AmbientChanges_FollowsTheNewOne()
    {
        // Arrange
        _machine.Update(new PetInputs(TimeSpan.Zero, Ambient: PetState.Focus));

        // Act
        var state = _machine.Update(new PetInputs(TimeSpan.Zero, Ambient: PetState.Alert));

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// An ambient state outranks a strained system, a pomodoro and development work.
    /// </summary>
    [Fact]
    public void Update_Ambient_OutranksTiredFocusAndCoding()
    {
        // Act
        var state = _machine.Update(new PetInputs(
            TimeSpan.Zero,
            IsSystemStrained: true,
            IsFocusing: true,
            IsDeveloping: true,
            Ambient: PetState.Alert));

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// A user who is away sleeps even while an ambient state applies.
    /// </summary>
    [Fact]
    public void Update_AmbientAndUserAway_Sleeps()
    {
        // Act
        var state = _machine.Update(new PetInputs(SleepAfter, Ambient: PetState.Focus));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// After sleeping, an ambient state that still applies is acted out again.
    /// </summary>
    [Fact]
    public void Update_AmbientAfterSleep_ResumesAmbient()
    {
        // Arrange
        var ambient = new PetInputs(TimeSpan.Zero, Ambient: PetState.Focus);
        _machine.Update(new PetInputs(SleepAfter, Ambient: PetState.Focus));
        _machine.Update(ambient);

        // Act
        var state = _machine.Update(ambient);

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// A moment outranks an ambient state, which resumes once the moment is over.
    /// </summary>
    [Fact]
    public void OnMoment_WhileAmbient_AmbientResumesAfterwards()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(3);
        var ambient = new PetInputs(TimeSpan.Zero, Ambient: PetState.Focus);
        _machine.Update(ambient);
        _machine.OnMoment(PetState.Happy, duration);

        // Act
        var during = _machine.Update(ambient);
        _clock.Advance(duration);
        var after = _machine.Update(ambient);

        // Assert
        during.Should().Be(PetState.Happy);
        after.Should().Be(PetState.Focus);
    }
}
