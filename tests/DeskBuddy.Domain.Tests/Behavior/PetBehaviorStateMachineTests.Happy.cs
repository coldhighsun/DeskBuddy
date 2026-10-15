namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Clicks and delight.
/// </content>
public partial class PetBehaviorStateMachineTests
{
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
}
