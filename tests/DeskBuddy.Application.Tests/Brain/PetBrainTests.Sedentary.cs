using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application.Tests.Brain;

/// <content>
/// Sedentary reminders.
/// </content>
public partial class PetBrainTests
{
    /// <summary>
    /// A user who keeps working long enough is reminded to get up, and the pet calls for attention.
    /// </summary>
    [Fact]
    public void Tick_WorkingLongEnough_ShowsSedentaryReminder()
    {
        // Act: the poll at minute 10 is the 11th.
        var message = RunMinutesUntilMessage(12);

        // Assert
        message.Should().NotBeNull();
        message!.Kind.Should().Be(PetMessageKind.SedentaryReminder);
        message.Actions.Should().Equal(MessageAction.SnoozeReminder, MessageAction.Dismiss);
        message.Duration.Should().Be(TimeSpan.FromMinutes(10));
        _brain.State.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// A user who is away does not get reminded.
    /// </summary>
    [Fact]
    public void Tick_UserAway_NoSedentaryReminder()
    {
        // Arrange
        _idle.IdleTime = TimeSpan.FromMinutes(6);

        // Act
        var message = RunMinutesUntilMessage(60);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// Snoozing makes the reminder return after the short snooze rather than the full interval.
    /// </summary>
    [Fact]
    public void Invoke_SnoozeReminder_RemindsAgainAfterSnooze()
    {
        // Arrange
        RunMinutesUntilMessage(12);
        _brain.Invoke(MessageAction.SnoozeReminder);

        // Act: snooze is 3 minutes.
        _clock.Advance(TimeSpan.FromMinutes(1));
        var early = RunMinutesUntilMessage(2);
        var message = RunMinutesUntilMessage(3);

        // Assert
        _brain.CurrentMessage.Should().BeSameAs(message);
        early.Should().BeNull();
        message.Should().NotBeNull();
        message!.Kind.Should().Be(PetMessageKind.SedentaryReminder);
    }

    /// <summary>
    /// Dismissing without snoozing waits the full interval before the next reminder.
    /// </summary>
    [Fact]
    public void Invoke_DismissReminder_NextReminderAfterFullInterval()
    {
        // Arrange
        RunMinutesUntilMessage(12);
        _brain.Invoke(MessageAction.Dismiss);
        _clock.Advance(TimeSpan.FromMinutes(1));

        // Act: 9 more minutes is not enough, one or two more is.
        var early = RunMinutesUntilMessage(9);
        var message = RunMinutesUntilMessage(3);

        // Assert
        early.Should().BeNull();
        message.Should().NotBeNull();
    }

    /// <summary>
    /// Reminders that come due while the user must not be disturbed do not pile up: only the latest one is shown
    /// afterwards.
    /// </summary>
    [Fact]
    public void Tick_SeveralRemindersDueWhileSuppressed_ShowsOnlyOne()
    {
        // Arrange: 35 minutes with a 10-minute interval makes three reminders come due.
        _fullscreen.DoNotDisturb = true;
        RunMinutesUntilMessage(35);
        _fullscreen.DoNotDisturb = false;

        // Act
        _brain.Tick();
        var first = _brain.CurrentMessage;
        _brain.Invoke(MessageAction.Dismiss);
        _brain.Tick();

        // Assert
        first.Should().NotBeNull();
        first!.Kind.Should().Be(PetMessageKind.SedentaryReminder);
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// A reminder held back while the user must not be disturbed is dropped once the user has taken a break.
    /// </summary>
    [Fact]
    public void Tick_UserRestsWhileReminderHeld_DropsReminder()
    {
        // Arrange
        _fullscreen.DoNotDisturb = true;
        RunMinutesUntilMessage(12);
        _idle.IdleTime = TimeSpan.FromMinutes(6);
        _brain.Tick();
        _idle.IdleTime = TimeSpan.Zero;
        _fullscreen.DoNotDisturb = false;

        // Act
        _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }
}
