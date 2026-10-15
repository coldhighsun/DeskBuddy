using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application.Tests;

/// <content>
/// Hiding for full-screen applications, do-not-disturb and manual hiding.
/// </content>
public partial class PetBrainTests
{
    /// <summary>
    /// While the user must not be disturbed the message is held back and the pet stays calm.
    /// </summary>
    [Fact]
    public void Tick_DoNotDisturb_HoldsMessageBack()
    {
        // Arrange
        _fullscreen.DoNotDisturb = true;
        _brain.Tick();
        _clock.Advance(ReminderDelay);

        // Act
        var state = _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
        state.Should().NotBe(PetState.Alert);
    }

    /// <summary>
    /// A held-back message appears once the user can be disturbed again.
    /// </summary>
    [Fact]
    public void Tick_DoNotDisturbEnds_ShowsHeldMessage()
    {
        // Arrange
        _fullscreen.DoNotDisturb = true;
        _brain.Tick();
        _clock.Advance(ReminderDelay);
        _brain.Tick();
        _fullscreen.DoNotDisturb = false;

        // Act
        var state = _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().NotBeNull();
        state.Should().Be(PetState.Alert);
    }

    /// <summary>
    /// Entering do-not-disturb while a message is showing hides it until the user can be disturbed again.
    /// </summary>
    [Fact]
    public void Tick_DoNotDisturbStartsWhileShowing_HidesMessage()
    {
        // Arrange
        _brain.Tick();
        _clock.Advance(ReminderDelay);
        _brain.Tick();
        _fullscreen.DoNotDisturb = true;

        // Act
        _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// A full-screen application hides the pet at once and announces it.
    /// </summary>
    [Fact]
    public void Tick_FullscreenStarts_HidesPetAndRaisesVisibilityChanged()
    {
        // Arrange
        var raised = 0;
        _brain.VisibilityChanged += (_, _) => raised++;
        _fullscreen.Level = DoNotDisturbLevel.Fullscreen;

        // Act
        _brain.Tick();

        // Assert
        _brain.Visibility.IsVisible.Should().BeFalse();
        raised.Should().Be(1);
    }

    /// <summary>
    /// A quiet do-not-disturb (such as Focus Assist) holds messages back but does not hide the pet.
    /// </summary>
    [Fact]
    public void Tick_QuietLevel_KeepsPetVisible()
    {
        // Arrange
        _fullscreen.Level = DoNotDisturbLevel.Quiet;

        // Act
        _brain.Tick();

        // Assert
        _brain.Visibility.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// The pet returns only after the full-screen application has been gone for the restore delay.
    /// </summary>
    [Fact]
    public void Tick_FullscreenEnds_PetReturnsAfterRestoreDelay()
    {
        // Arrange
        _fullscreen.Level = DoNotDisturbLevel.Fullscreen;
        _brain.Tick();
        _fullscreen.Level = DoNotDisturbLevel.None;
        _brain.Tick();

        // Act
        var visibleTooSoon = _brain.Visibility.IsVisible;
        _clock.Advance(RestoreDelay);
        _brain.Tick();

        // Assert
        visibleTooSoon.Should().BeFalse();
        _brain.Visibility.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// A message that comes due while the pet is hidden by the user is shown once the pet is back.
    /// </summary>
    [Fact]
    public void Tick_MessageDueWhilePetHidden_ShownWhenPetReturns()
    {
        // Arrange
        _brain.SetManuallyHidden(true);
        _brain.Tick();
        _clock.Advance(ReminderDelay);
        _brain.Tick();
        var shownWhileHidden = _brain.CurrentMessage;

        // Act
        _brain.SetManuallyHidden(false);
        _brain.Tick();

        // Assert
        shownWhileHidden.Should().BeNull();
        _brain.CurrentMessage.Should().NotBeNull();
    }

    /// <summary>
    /// Hiding and showing at the user's request is announced, and toggling twice restores the pet.
    /// </summary>
    [Fact]
    public void ToggleManuallyHidden_Twice_HidesThenShowsAndRaisesEachTime()
    {
        // Arrange
        var raised = 0;
        _brain.VisibilityChanged += (_, _) => raised++;

        // Act
        _brain.ToggleManuallyHidden();
        var hidden = _brain.Visibility.IsVisible;
        _brain.ToggleManuallyHidden();

        // Assert
        hidden.Should().BeFalse();
        _brain.Visibility.IsVisible.Should().BeTrue();
        raised.Should().Be(2);
    }

    /// <summary>
    /// Hiding a pet that is already hidden does not announce anything.
    /// </summary>
    [Fact]
    public void SetManuallyHidden_AlreadyHidden_DoesNotRaise()
    {
        // Arrange
        _brain.SetManuallyHidden(true);
        var raised = 0;
        _brain.VisibilityChanged += (_, _) => raised++;

        // Act
        _brain.SetManuallyHidden(true);

        // Assert
        raised.Should().Be(0);
    }
}
