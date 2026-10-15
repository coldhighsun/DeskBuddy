namespace DeskBuddy.Domain.Tests.Behavior;

/// <content>
/// Dragging, falling, gliding and crawling.
/// </content>
public partial class PetBehaviorStateMachineTests
{
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
    /// When the falling pet's umbrella opens, it holds it: the state changes from falling to gliding.
    /// </summary>
    [Fact]
    public void OnFallOpened_WhileFalling_EntersGlide()
    {
        // Arrange
        _machine.OnFallStarted();

        // Act
        _machine.OnFallOpened();

        // Assert
        _machine.State.Should().Be(PetState.Glide);
    }

    /// <summary>
    /// A pet that is not falling has no umbrella to open.
    /// </summary>
    [Fact]
    public void OnFallOpened_NotFalling_DoesNotChangeState()
    {
        // Act
        _machine.OnFallOpened();

        // Assert
        _machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A pet gliding down with its umbrella keeps at it however long it takes, and lands in the idle state.
    /// </summary>
    [Fact]
    public void Update_WhileGliding_StaysUntilLanded()
    {
        // Arrange
        _machine.OnFallStarted();
        _machine.OnFallOpened();
        _clock.Advance(TimeSpan.FromHours(1));

        // Act
        var whileGliding = _machine.Update(Active);
        _machine.OnFallEnded();

        // Assert
        whileGliding.Should().Be(PetState.Glide);
        _machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A glide that is only acted out, as when a pet's pictures are previewed, ends after its time like any other
    /// moment instead of lasting until a landing that never comes.
    /// </summary>
    [Fact]
    public void Update_GlideActedOutForAMoment_EndsAfterTheMoment()
    {
        // Arrange
        _machine.OnMoment(PetState.Glide, TimeSpan.FromSeconds(5));
        var during = _machine.Update(Active);
        _clock.Advance(TimeSpan.FromSeconds(6));

        // Act
        var after = _machine.Update(Active);

        // Assert
        during.Should().Be(PetState.Glide);
        after.Should().Be(PetState.Idle);
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
    /// A pet crawling along the edges of the screen keeps at it, however long it takes.
    /// </summary>
    /// <param name="state">
    /// The crawling state.
    /// </param>
    [Theory]
    [InlineData(PetState.Climb)]
    [InlineData(PetState.ClimbDown)]
    [InlineData(PetState.Ceiling)]
    public void Update_WhileCrawling_StaysInState(PetState state)
    {
        // Arrange
        _machine.OnCrawlStarted(state);
        _clock.Advance(TimeSpan.FromHours(1));

        // Act
        var result = _machine.Update(Active);

        // Assert
        result.Should().Be(state);
    }

    /// <summary>
    /// A state that is not a way of crawling does not start a crawl.
    /// </summary>
    [Fact]
    public void OnCrawlStarted_OtherState_IsIgnored()
    {
        // Act
        _machine.OnCrawlStarted(PetState.Happy);

        // Assert
        _machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A falling or dragged pet does not start crawling.
    /// </summary>
    [Fact]
    public void OnCrawlStarted_WhileFalling_IsIgnored()
    {
        // Arrange
        _machine.OnFallStarted();

        // Act
        _machine.OnCrawlStarted(PetState.Climb);

        // Assert
        _machine.State.Should().Be(PetState.Fall);
    }

    /// <summary>
    /// The end of the crawl leaves the pet standing.
    /// </summary>
    [Fact]
    public void OnCrawlEnded_WhileCrawling_EntersIdle()
    {
        // Arrange
        _machine.OnCrawlStarted(PetState.Ceiling);

        // Act
        _machine.OnCrawlEnded();

        // Assert
        _machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Ending a crawl that never started does nothing.
    /// </summary>
    [Fact]
    public void OnCrawlEnded_NotCrawling_DoesNotChangeState()
    {
        // Arrange
        _machine.OnDragStarted();

        // Act
        _machine.OnCrawlEnded();

        // Assert
        _machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// Grabbing a crawling pet drags it.
    /// </summary>
    [Fact]
    public void OnDragStarted_WhileCrawling_EntersDrag()
    {
        // Arrange
        _machine.OnCrawlStarted(PetState.Climb);

        // Act
        _machine.OnDragStarted();

        // Assert
        _machine.State.Should().Be(PetState.Drag);
    }

    /// <summary>
    /// An alert still takes the pet's attention away from crawling.
    /// </summary>
    [Fact]
    public void Update_AlertWhileCrawling_EntersAlert()
    {
        // Arrange
        _machine.OnCrawlStarted(PetState.Climb);

        // Act
        var state = _machine.Update(Alerting);

        // Assert
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// A reset puts the pet back to standing, whatever it was doing.
    /// </summary>
    /// <param name="state">
    /// The state the pet was in.
    /// </param>
    [Theory]
    [InlineData(PetState.Climb)]
    [InlineData(PetState.Ceiling)]
    [InlineData(PetState.Fall)]
    [InlineData(PetState.Drag)]
    public void Reset_InAnyState_EntersIdle(PetState state)
    {
        // Arrange
        if (state == PetState.Fall)
        {
            _machine.OnFallStarted();
        }
        else if (state == PetState.Drag)
        {
            _machine.OnDragStarted();
        }
        else
        {
            _machine.OnCrawlStarted(state);
        }

        // Act
        _machine.Reset();

        // Assert
        _machine.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A moment that was acted out is over after a reset.
    /// </summary>
    [Fact]
    public void Reset_WhileActingOutMoment_EndsTheMoment()
    {
        // Arrange
        _machine.OnMoment(PetState.Hang, TimeSpan.FromMinutes(5));

        // Act
        _machine.Reset();
        var state = _machine.Update(Active);

        // Assert
        state.Should().Be(PetState.Idle);
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
}
