namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Alerts.
/// </content>
public partial class PetBehaviorStateMachineTests
{
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
    /// An alert outranks sleep and tiredness.
    /// </summary>
    [Fact]
    public void Update_AlertWithOtherConditions_AlertWins()
    {
        // Act
        var state = _machine.Update(new PetInputs(SleepAfter, IsSystemStrained: true, HasAlert: true));

        // Assert
        state.Should().Be(PetState.Alert);
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
}
