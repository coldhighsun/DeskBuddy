namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Moments acted out on request.
/// </content>
public partial class PetBehaviorStateMachineTests
{
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
    /// Clicks and short scenes do not interrupt a crawl.
    /// </summary>
    [Fact]
    public void OnMoment_WhileCrawling_IsIgnored()
    {
        // Arrange
        _machine.OnCrawlStarted(PetState.Climb);

        // Act
        _machine.OnMoment(PetState.Dizzy, TimeSpan.FromSeconds(2));
        _machine.OnClicked();

        // Assert
        _machine.State.Should().Be(PetState.Climb);
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
    /// A moment holds even while the pet would otherwise be Tired or Coding.
    /// </summary>
    [Fact]
    public void OnMoment_WhileLowerRankedInputsApply_StaysUntilItEnds()
    {
        // Arrange
        _machine.OnMoment(PetState.Happy, TimeSpan.FromSeconds(3));

        // Act
        var state = _machine.Update(new PetInputs(TimeSpan.Zero, IsSystemStrained: true, IsDeveloping: true));

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
