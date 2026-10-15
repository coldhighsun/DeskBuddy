namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Tiredness under system load.
/// </content>
public partial class PetBehaviorStateMachineTests
{
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
}
