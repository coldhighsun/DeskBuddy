namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Short walks out of states that would otherwise hold the pet still for hours.
/// </content>
public partial class PetBehaviorStateMachineTests
{
    /// <summary>
    /// Puts the machine into a state with the given inputs and lets it stay there until it is time for a stroll.
    /// </summary>
    /// <param name="inputs">
    /// The inputs that hold the pet in its state.
    /// </param>
    /// <returns>
    /// The state after the stroll began.
    /// </returns>
    private PetState StartStroll(PetInputs inputs)
    {
        _machine.Update(inputs);
        _clock.Advance(StrollInterval);
        return _machine.Update(inputs);
    }

    /// <summary>
    /// A pet that has been coding for the stroll interval gets up for a walk.
    /// </summary>
    [Fact]
    public void Update_DevelopingForStrollInterval_Walks()
    {
        // Act
        var state = StartStroll(Developing);

        // Assert
        state.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// After its walk the pet sits down to code again.
    /// </summary>
    [Fact]
    public void Update_StrollIsOver_ReturnsToCoding()
    {
        // Arrange
        StartStroll(Developing);

        // Act
        _clock.Advance(WalkDuration);
        var state = _machine.Update(Developing);

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// The pet keeps walking for the whole stroll and is not put back to its desk early.
    /// </summary>
    [Fact]
    public void Update_StrollUnderWay_KeepsWalking()
    {
        // Arrange
        StartStroll(Developing);

        // Act
        _clock.Advance(WalkDuration - TimeSpan.FromSeconds(1));
        var state = _machine.Update(Developing);

        // Assert
        state.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// The next stroll comes after another interval, not at once.
    /// </summary>
    [Fact]
    public void Update_StrollIsOver_NextStrollComesAfterAnotherInterval()
    {
        // Arrange
        StartStroll(Developing);
        _clock.Advance(WalkDuration);
        _machine.Update(Developing);

        // Act
        _clock.Advance(StrollInterval - TimeSpan.FromSeconds(1));
        var before = _machine.Update(Developing);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var after = _machine.Update(Developing);

        // Assert
        before.Should().Be(PetState.Coding);
        after.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// With the option switched off the pet stays at its computer.
    /// </summary>
    [Fact]
    public void Update_StrollDisabled_StaysCoding()
    {
        // Arrange
        _options.StrollEnabled = false;

        // Act
        var state = StartStroll(Developing);

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// Switching the option off during a stroll lets the walk finish, then the pet returns and does not stroll again.
    /// </summary>
    [Fact]
    public void Update_StrollDisabledDuringStroll_FinishesWalkAndStays()
    {
        // Arrange
        StartStroll(Developing);
        _options.StrollEnabled = false;

        // Act
        _clock.Advance(WalkDuration);
        var back = _machine.Update(Developing);
        _clock.Advance(StrollInterval);
        var later = _machine.Update(Developing);

        // Assert
        back.Should().Be(PetState.Coding);
        later.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// Long-lived ambient states take a stroll as well and come back to the same state.
    /// </summary>
    /// <param name="ambient">
    /// The ambient state that holds the pet.
    /// </param>
    [Theory]
    [InlineData(PetState.Meeting)]
    [InlineData(PetState.Chatting)]
    [InlineData(PetState.Building)]
    [InlineData(PetState.Reading)]
    [InlineData(PetState.NightCoding)]
    public void Update_AmbientThatCanStroll_WalksAndReturns(PetState ambient)
    {
        // Arrange
        var inputs = new PetInputs(TimeSpan.Zero, Ambient: ambient);

        // Act
        var during = StartStroll(inputs);
        _clock.Advance(WalkDuration);
        var after = _machine.Update(inputs);

        // Assert
        during.Should().Be(PetState.Walk);
        after.Should().Be(ambient);
    }

    /// <summary>
    /// A pet that is low on battery stays where it is.
    /// </summary>
    [Fact]
    public void Update_LowBattery_NeverStrolls()
    {
        // Act
        var state = StartStroll(new PetInputs(TimeSpan.Zero, Ambient: PetState.LowBattery));

        // Assert
        state.Should().Be(PetState.LowBattery);
    }

    /// <summary>
    /// A tired pet stays where it is.
    /// </summary>
    [Fact]
    public void Update_Tired_NeverStrolls()
    {
        // Act
        var state = StartStroll(Strained);

        // Assert
        state.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// If the reason for the state goes away during the stroll, the pet finishes its walk like any other and then rests.
    /// </summary>
    [Fact]
    public void Update_StateEndsDuringStroll_FinishesWalkThenIdle()
    {
        // Arrange
        StartStroll(Developing);

        // Act
        var during = _machine.Update(Active);
        _clock.Advance(WalkDuration);
        var after = _machine.Update(Active);

        // Assert
        during.Should().Be(PetState.Walk);
        after.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// If another ambient state takes over during the stroll, the pet goes into it at once.
    /// </summary>
    [Fact]
    public void Update_AmbientChangesDuringStroll_FollowsTheNewOne()
    {
        // Arrange
        StartStroll(new PetInputs(TimeSpan.Zero, Ambient: PetState.Meeting));

        // Act
        var state = _machine.Update(new PetInputs(TimeSpan.Zero, Ambient: PetState.Focus));

        // Assert
        state.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// A message on screen calls the pet away from its stroll; once it is gone the pet takes up its state again and does
    /// not carry on walking.
    /// </summary>
    [Fact]
    public void Update_AlertDuringStroll_AlertsThenCodesAgain()
    {
        // Arrange
        StartStroll(Developing);

        // Act
        var alerting = _machine.Update(new PetInputs(TimeSpan.Zero, HasAlert: true, IsDeveloping: true));
        var after = _machine.Update(Developing);

        // Assert
        alerting.Should().Be(PetState.Alert);
        after.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// A user who walks away during the stroll puts the pet to sleep.
    /// </summary>
    [Fact]
    public void Update_UserAwayDuringStroll_Sleeps()
    {
        // Arrange
        StartStroll(Developing);

        // Act
        var state = _machine.Update(new PetInputs(SleepAfter, IsDeveloping: true));

        // Assert
        state.Should().Be(PetState.Sleep);
    }

    /// <summary>
    /// A pet picked up during the stroll is dragged, and put down it is idle: the stroll is forgotten.
    /// </summary>
    [Fact]
    public void OnDragEnded_DuringStroll_ReturnsToIdle()
    {
        // Arrange
        StartStroll(Developing);
        _machine.OnDragStarted();

        // Act
        _machine.OnDragEnded();

        // Assert
        _machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A pet that is clicked during the stroll is happy and then takes up its state again, as it would anywhere else.
    /// </summary>
    [Fact]
    public void OnClicked_DuringStroll_HappyThenCodingAgain()
    {
        // Arrange
        StartStroll(Developing);
        _machine.OnClicked();
        _machine.State.Should().Be(PetState.Happy);
        _clock.Advance(HappyDuration);

        // Act
        var state = _machine.Update(Developing);

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// A hop in the middle of a stroll goes back to the walk and then to the desk.
    /// </summary>
    [Fact]
    public void OnHopEnded_DuringStrollWithTimeLeft_WalksOnThenReturnsToCoding()
    {
        // Arrange
        var machine = CreateStrollingHopper(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        _clock.Advance(TimeSpan.FromSeconds(1));
        machine.Update(Developing).Should().Be(PetState.Hop);
        _clock.Advance(TimeSpan.FromSeconds(2));

        // Act
        machine.OnHopEnded();
        var walking = machine.State;
        _clock.Advance(WalkDuration);
        var after = machine.Update(Developing);

        // Assert
        walking.Should().Be(PetState.Walk);
        after.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// A hop that runs past the end of the stroll leaves the pet standing, as any walk does, until the next update sits
    /// it down at its desk again.
    /// </summary>
    [Fact]
    public void OnHopEnded_DuringStrollWithNoTimeLeft_StandsThenReturnsToCoding()
    {
        // Arrange
        var machine = CreateStrollingHopper(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        _clock.Advance(TimeSpan.FromSeconds(1));
        machine.Update(Developing).Should().Be(PetState.Hop);
        _clock.Advance(WalkDuration);

        // Act
        machine.OnHopEnded();
        var standing = machine.State;
        var after = machine.Update(Developing);

        // Assert
        standing.Should().Be(PetState.Idle);
        after.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// If the state the pet left ends while it hops, it rests when the hop is over instead of going back to a state that
    /// is no longer there.
    /// </summary>
    [Fact]
    public void OnHopEnded_StateEndedDuringHop_Idles()
    {
        // Arrange
        var machine = CreateStrollingHopper(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        _clock.Advance(TimeSpan.FromSeconds(1));
        machine.Update(Developing).Should().Be(PetState.Hop);
        machine.Update(Active).Should().Be(PetState.Hop);
        _clock.Advance(WalkDuration);

        // Act
        machine.OnHopEnded();
        var standing = machine.State;
        var after = machine.Update(Active);

        // Assert
        standing.Should().Be(PetState.Idle);
        after.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A hop refused during a stroll (no room near the screen edge) is given up and another time is picked: the pet is
    /// not asked again at once, and hops when the new time comes and there is room.
    /// </summary>
    [Fact]
    public void Update_HopRefusedDuringStroll_GivesUpAndPicksAnotherTime()
    {
        // Arrange
        var machine = CreateStrollingHopper(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        var asked = 0;
        var room = false;
        machine.CanHopNow = () =>
        {
            asked++;
            return room;
        };
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        var refused = machine.Update(Developing);
        _clock.Advance(TimeSpan.FromSeconds(0.5));
        var stillWaiting = machine.Update(Developing);
        var askedBeforeTheNewTime = asked;
        room = true;
        _clock.Advance(TimeSpan.FromSeconds(0.5));
        var hopped = machine.Update(Developing);

        // Assert
        refused.Should().Be(PetState.Walk);
        stillWaiting.Should().Be(PetState.Walk);
        askedBeforeTheNewTime.Should().Be(1);
        hopped.Should().Be(PetState.Hop);
    }

    /// <summary>
    /// Makes a machine, coding, that has just set off on a stroll and is to hop after a fixed time.
    /// </summary>
    /// <param name="hopAfter">
    /// How long into the walk the pet hops.
    /// </param>
    /// <param name="hopDuration">
    /// How long the hop lasts.
    /// </param>
    /// <returns>
    /// The machine, in the first moment of the stroll.
    /// </returns>
    private PetBehaviorStateMachine CreateStrollingHopper(TimeSpan hopAfter, TimeSpan hopDuration)
    {
        var options = CreateOptions();
        options.HopIntervalMin = hopAfter;
        options.HopIntervalMax = hopAfter;
        var machine = new PetBehaviorStateMachine(options, _clock, new Random(42)) { HopDuration = hopDuration };
        machine.Update(Developing);
        _clock.Advance(StrollInterval);
        machine.Update(Developing).Should().Be(PetState.Walk);
        return machine;
    }
}
