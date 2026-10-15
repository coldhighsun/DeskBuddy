using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application.Tests;

/// <content>
/// Showing and dismissing messages.
/// </content>
public partial class PetBrainTests
{
    /// <summary>
    /// A message fades on its own and the pet settles down again.
    /// </summary>
    [Fact]
    public void Tick_MessageTimesOut_AlertEnds()
    {
        // Arrange
        _brain.Tick();
        _clock.Advance(ReminderDelay);
        _brain.Tick();

        // Act
        _clock.Advance(TimeSpan.FromSeconds(31));
        var state = _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
        state.Should().NotBe(PetState.Alert);
    }

    /// <summary>
    /// Dismiss only closes the bubble.
    /// </summary>
    [Fact]
    public void Invoke_Dismiss_OnlyClosesMessage()
    {
        // Arrange
        _brain.Tick();
        _clock.Advance(ReminderDelay);
        _brain.Tick();

        // Act
        _brain.Invoke(MessageAction.Dismiss);

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// Clicking the pet acknowledges the message on screen.
    /// </summary>
    [Fact]
    public void OnClicked_WithMessageShowing_DismissesIt()
    {
        // Arrange
        _brain.Tick();
        _clock.Advance(ReminderDelay);
        _brain.Tick();

        // Act
        _brain.OnClicked();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
        _brain.State.Should().Be(PetState.Happy);
    }

    /// <summary>
    /// Showing and closing a message raises <see cref="PetBrain.MessageChanged"/> each time.
    /// </summary>
    [Fact]
    public void MessageChanged_ShownThenDismissed_RaisedTwice()
    {
        // Arrange
        var raised = 0;
        _brain.MessageChanged += (_, _) => raised++;
        _brain.Tick();
        _clock.Advance(ReminderDelay);

        // Act
        _brain.Tick();
        _brain.DismissMessage();

        // Assert
        raised.Should().Be(2);
    }

    /// <summary>
    /// Dismissing with nothing on screen raises no event.
    /// </summary>
    [Fact]
    public void DismissMessage_NothingShown_RaisesNothing()
    {
        // Arrange
        var raised = 0;
        _brain.MessageChanged += (_, _) => raised++;

        // Act
        _brain.DismissMessage();

        // Assert
        raised.Should().Be(0);
    }
}
