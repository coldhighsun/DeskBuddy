namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Coding while the user develops.
/// </content>
public partial class PetBehaviorStateMachineTests
{
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
    /// A pet at its computer stays there instead of wandering off until it is time for a stroll.
    /// </summary>
    [Fact]
    public void Update_DevelopingBeforeStrollInterval_StaysCoding()
    {
        // Arrange
        _machine.Update(Developing);

        // Act
        _clock.Advance(StrollInterval - TimeSpan.FromSeconds(1));
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
}
