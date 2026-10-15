namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Ambient scenarios.
/// </content>
public partial class PetBehaviorStateMachineTests
{
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
    /// An ambient state outranks a strained system and development work.
    /// </summary>
    [Fact]
    public void Update_Ambient_OutranksTiredAndCoding()
    {
        // Act
        var state = _machine.Update(new PetInputs(
            TimeSpan.Zero,
            IsSystemStrained: true,
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
}
