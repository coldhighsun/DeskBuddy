using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskBuddy.App.Rendering;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Windows;

/// <content>
/// The speech bubble that follows the pet.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// The speech bubble, created the first time a message needs showing.
    /// </summary>
    private BubbleWindow? _bubble;

    /// <summary>
    /// Shows, updates or hides the speech bubble to match the brain's current message. The bubble stays hidden while
    /// the pet itself is hidden.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private void OnMessageChanged(object? sender, EventArgs e)
    {
        var message = _brain.CurrentMessage;
        if (message is null || !IsVisible)
        {
            _bubble?.Hide();
            return;
        }

        if (_bubble is null)
        {
            _bubble = new BubbleWindow();
            _bubble.ActionInvoked += OnBubbleActionInvoked;
        }

        _bubble.SetContent(MessagePresenter.Present(message));
        PositionBubble(_bubble);
        if (!_bubble.IsVisible)
        {
            _bubble.Show();
        }
    }

    /// <summary>
    /// Carries out the action the user chose in the speech bubble.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="action">The chosen action.</param>
    private void OnBubbleActionInvoked(object? sender, MessageAction action)
    {
        _brain.Invoke(action);
        RefreshState();
    }

    /// <summary>
    /// Keeps the speech bubble attached to the pet while the pet moves.
    /// </summary>
    private void RepositionBubble()
    {
        if (_bubble is { IsVisible: true })
        {
            PositionBubble(_bubble);
        }
    }

    /// <summary>
    /// Puts the speech bubble next to the sprite being shown: above it, or below it when there is no room above.
    /// </summary>
    /// <param name="bubble">The bubble to place.</param>
    private void PositionBubble(BubbleWindow bubble)
    {
        var mask = _frames.GetMask(_player.CurrentFrameId);
        bubble.PositionNear(this, mask.TopOpaqueFraction(), mask.BottomOpaqueFraction());
    }
}
