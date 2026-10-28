using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application.Tests;

/// <content>
/// Unprompted remarks.
/// </content>
public partial class PetBrainTests
{
    /// <summary>
    /// A chatting pet says something now and then, and it is shown briefly without raising the alert.
    /// </summary>
    [Fact]
    public void Tick_ChatterEnabled_ShowsShortRemarkWithoutAlert()
    {
        // Act
        var message = RunUntilChatter(20);

        // Assert
        message.Should().NotBeNull();
        message!.Kind.Should().Be(PetMessageKind.Chatter);
        message.Actions.Should().BeEmpty();
        message.Lifetime.Should().Be(TimeSpan.FromSeconds(8));
        _brain.State.Should().NotBe(PetState.Alert);
    }

    /// <summary>
    /// A casual remark fades on its own after its short lifetime.
    /// </summary>
    [Fact]
    public void Tick_ChatterShown_DisappearsAfterLifetime()
    {
        // Arrange
        RunUntilChatter(20);

        // Act
        _clock.Advance(TimeSpan.FromSeconds(9));
        _idle.IdleTime = TimeSpan.Zero;
        _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// The pet stays quiet while the chatter policy is off, however long it waits.
    /// </summary>
    [Fact]
    public void Tick_ChatterDisabled_NeverSpeaks()
    {
        // Act
        var message = RunUntilChatter(60, chatterEnabled: false);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// A falling pet makes no casual remark.
    /// </summary>
    [Fact]
    public void Tick_Falling_NoChatter()
    {
        // Arrange
        _brain.OnFallStarted();

        // Act
        var message = RunUntilChatter(60);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// A casual remark on screen when the pet starts to fall is dropped and does not come back after landing.
    /// </summary>
    [Fact]
    public void Tick_FallStartsWhileChatterShowing_DropsItForGood()
    {
        // Arrange
        RunUntilChatter(20);
        _brain.OnFallStarted();
        _brain.Tick();
        _brain.OnFallEnded();

        // Act
        _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// A casual remark on screen when do-not-disturb starts is dropped and does not come back when it ends.
    /// </summary>
    [Fact]
    public void Tick_DoNotDisturbStartsWhileChatterShowing_DropsItForGood()
    {
        // Arrange
        RunUntilChatter(20);
        _fullscreen.DoNotDisturb = true;
        _brain.Tick();
        _fullscreen.DoNotDisturb = false;

        // Act
        _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// A user who is away is not spoken to.
    /// </summary>
    [Fact]
    public void Tick_UserAway_NoChatter()
    {
        // Act
        var message = RunUntilChatter(60, idle: TimeSpan.FromMinutes(2));

        // Assert
        message.Should().BeNull();
    }
}
