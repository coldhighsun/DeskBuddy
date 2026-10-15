using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application.Tests;

/// <content>
/// Calls forwarded to the behavior state machine.
/// </content>
public partial class PetBrainTests
{
    /// <summary>
    /// Drag notifications reach the state machine.
    /// </summary>
    [Fact]
    public void OnDragStartedAndEnded_Forwarded_ToStateMachine()
    {
        // Act & Assert
        _brain.OnDragStarted();
        _brain.State.Should().Be(PetState.Drag);

        _brain.OnDragEnded();
        _brain.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Fall notifications reach the state machine.
    /// </summary>
    [Fact]
    public void OnFallStartedAndEnded_Forwarded_ToStateMachine()
    {
        // Act & Assert
        _brain.OnFallStarted();
        _brain.State.Should().Be(PetState.Fall);

        _brain.OnFallEnded();
        _brain.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// The opening of the umbrella reaches the state machine.
    /// </summary>
    [Fact]
    public void OnFallOpened_Forwarded_ToStateMachine()
    {
        // Arrange
        _brain.OnFallStarted();

        // Act
        _brain.OnFallOpened();

        // Assert
        _brain.State.Should().Be(PetState.Glide);
    }

    /// <summary>
    /// Crawl notifications reach the state machine.
    /// </summary>
    [Fact]
    public void OnCrawlStartedAndEnded_Forwarded_ToStateMachine()
    {
        // Act & Assert
        _brain.OnCrawlStarted(PetState.Ceiling);
        _brain.State.Should().Be(PetState.Ceiling);

        _brain.OnCrawlEnded();
        _brain.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A reset reaches the state machine and ends whatever the pet was doing.
    /// </summary>
    [Fact]
    public void ResetState_WhileFalling_PetStandsAgain()
    {
        // Arrange
        _brain.OnFallStarted();

        // Act
        _brain.ResetState();

        // Assert
        _brain.State.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Click notifications reach the state machine.
    /// </summary>
    [Fact]
    public void OnClicked_Forwarded_PetIsHappy()
    {
        // Act
        _brain.OnClicked();

        // Assert
        _brain.State.Should().Be(PetState.Happy);
    }

    /// <summary>
    /// A new brain does not hop, as it does not know of a pet that does.
    /// </summary>
    [Fact]
    public void HopDuration_Default_IsNull()
    {
        // Assert
        _brain.HopDuration.Should().BeNull();
    }

    /// <summary>
    /// The hop duration the window sets from the pet's own animation reaches the state machine, and clearing it again
    /// (a pet that does not hop) reaches it too.
    /// </summary>
    [Fact]
    public void HopDuration_SetAndCleared_IsPassedOn()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(625);

        // Act
        _brain.HopDuration = duration;
        var whileSet = _brain.HopDuration;
        _brain.HopDuration = null;

        // Assert
        whileSet.Should().Be(duration);
        _brain.HopDuration.Should().BeNull();
    }

    /// <summary>
    /// Ending a hop that is not going on leaves the brain as it is.
    /// </summary>
    [Fact]
    public void OnHopEnded_NotHopping_IsIgnored()
    {
        // Arrange
        var before = _brain.State;

        // Act
        _brain.OnHopEnded();

        // Assert
        _brain.State.Should().Be(before);
    }

    /// <summary>
    /// A new brain has no question for hops, and the one the window sets reaches the state machine.
    /// </summary>
    [Fact]
    public void CanHopNow_SetAndRead_IsPassedOn()
    {
        // Arrange
        var byDefault = _brain.CanHopNow;
        Func<bool> question = () => false;

        // Act
        _brain.CanHopNow = question;

        // Assert
        byDefault.Should().BeNull();
        _brain.CanHopNow.Should().BeSameAs(question);
    }
}
