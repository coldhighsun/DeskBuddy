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
