namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Hopping while walking.
/// </content>
public partial class PetBehaviorStateMachineTests
{
    /// <summary>
    /// A walking pet hops once its hop time has come.
    /// </summary>
    [Fact]
    public void Update_WalkingAndHopTimeCame_Hops()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));
        _clock.Advance(TimeSpan.FromSeconds(2));

        // Act
        var state = machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Hop);
    }

    /// <summary>
    /// Before its hop time has come the pet just walks.
    /// </summary>
    [Fact]
    public void Update_WalkingBeforeHopTime_KeepsWalking()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        var state = machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// A hop is held until it is played out, however long that takes: updates meanwhile leave the pet hopping.
    /// </summary>
    [Fact]
    public void Update_Hopping_StaysHoppingUntilTheHopEnds()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));
        _clock.Advance(TimeSpan.FromSeconds(2));
        machine.Update(Active);
        _clock.Advance(TimeSpan.FromMilliseconds(1500));

        // Act
        var state = machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Hop);
    }

    /// <summary>
    /// After a hop the pet walks on in the same direction, and the walk ends when it would have without the hop.
    /// </summary>
    [Fact]
    public void OnHopEnded_WalkLeft_WalksOnInTheSameDirectionUntilTheWalkEnds()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));
        var direction = machine.WalkDirection;
        _clock.Advance(TimeSpan.FromSeconds(2));
        machine.Update(Active);
        _clock.Advance(TimeSpan.FromMilliseconds(600));

        // Act
        machine.OnHopEnded();
        var afterHop = machine.State;
        var directionAfterHop = machine.WalkDirection;
        _clock.Advance(WalkDuration - TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(600));
        var afterWalk = machine.Update(Active);

        // Assert
        afterHop.Should().Be(PetState.Walk);
        directionAfterHop.Should().Be(direction);
        afterWalk.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// If the walk ran out while the pet hopped, the pet stands instead of walking for one more update.
    /// </summary>
    [Fact]
    public void OnHopEnded_WalkOver_GoesStraightToIdle()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(4.3), TimeSpan.FromMilliseconds(600));
        _clock.Advance(TimeSpan.FromSeconds(4.3));
        machine.Update(Active);
        _clock.Advance(TimeSpan.FromSeconds(2));

        // Act
        machine.OnHopEnded();

        // Assert
        machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Ending a hop that is not going on changes nothing.
    /// </summary>
    [Fact]
    public void OnHopEnded_NotHopping_IsIgnored()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));

        // Act
        machine.OnHopEnded();

        // Assert
        machine.State.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// A hop that would not be over before the walk ends is not made.
    /// </summary>
    [Fact]
    public void Update_HopTimeTooCloseToTheEndOfTheWalk_DoesNotHop()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(4.8), TimeSpan.FromMilliseconds(600));
        _clock.Advance(TimeSpan.FromSeconds(4.8));

        // Act
        var state = machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// A standing pet does not hop.
    /// </summary>
    [Fact]
    public void Update_Idle_NeverHops()
    {
        // Arrange
        var options = new PetBehaviorOptions
        {
            IdleDwellMin = IdleDwell,
            IdleDwellMax = IdleDwell,
            HopIntervalMin = TimeSpan.Zero,
            HopIntervalMax = TimeSpan.Zero,
        };
        var machine = new PetBehaviorStateMachine(options, _clock, new Random(42)) { HopDuration = TimeSpan.FromMilliseconds(600) };
        _clock.Advance(IdleDwell - TimeSpan.FromSeconds(1));

        // Act
        var state = machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// An alert cuts a hop short.
    /// </summary>
    [Fact]
    public void Update_HoppingAndAlert_EntersAlert()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));
        _clock.Advance(TimeSpan.FromSeconds(2));
        machine.Update(Active);

        // Act
        var state = machine.Update(Alerting);

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// Picking up a hopping pet drags it.
    /// </summary>
    [Fact]
    public void OnDragStarted_Hopping_EntersDrag()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));
        _clock.Advance(TimeSpan.FromSeconds(2));
        machine.Update(Active);

        // Act
        machine.OnDragStarted();

        // Assert
        machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// A pet that has no hop of its own (no hop duration) never hops, however long it walks.
    /// </summary>
    [Fact]
    public void Update_WalkingAndNoHopDuration_NeverHops()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));
        machine.HopDuration = null;
        _clock.Advance(TimeSpan.FromSeconds(2));

        // Act
        var state = machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// The pet is asked whether it may hop only when its hop time has come; if it may not (no room), it does not hop and
    /// picks another time at random, instead of asking again at every update.
    /// </summary>
    [Fact]
    public void Update_HopRefused_GivesUpAndPicksAnotherTime()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(600));
        var asked = 0;
        var room = false;
        machine.CanHopNow = () =>
        {
            asked++;
            return room;
        };
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        var refused = machine.Update(Active);
        _clock.Advance(TimeSpan.FromSeconds(0.5));
        var stillWaiting = machine.Update(Active);
        var askedBeforeTheNewTime = asked;
        room = true;
        _clock.Advance(TimeSpan.FromSeconds(0.5));
        var hopped = machine.Update(Active);

        // Assert
        refused.Should().Be(PetState.Walk);
        stillWaiting.Should().Be(PetState.Walk);
        askedBeforeTheNewTime.Should().Be(1);
        hopped.Should().Be(PetState.Hop);
        asked.Should().Be(2);
    }

    /// <summary>
    /// A pet is not asked whether it may hop when it would not hop anyway (it does not walk, or has no hop of its own).
    /// </summary>
    [Fact]
    public void Update_NoHopDue_DoesNotAsk()
    {
        // Arrange
        var machine = CreateHoppingMachine(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(600));
        var asked = 0;
        machine.CanHopNow = () =>
        {
            asked++;
            return true;
        };
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        machine.Update(Active);

        // Assert
        asked.Should().Be(0);
    }
}
