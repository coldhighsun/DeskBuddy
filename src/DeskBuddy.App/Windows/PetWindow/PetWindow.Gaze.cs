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
/// Following the pointer with the pet's eyes.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// How often the mouse pointer is looked at to turn the eyes towards it; the eyes need no finer a follow than this.
    /// </summary>
    private static readonly TimeSpan PointerInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Timer that reads the mouse pointer for the gaze; runs only while the pet is on screen and stands around.
    /// </summary>
    private readonly DispatcherTimer _pointerTimer = new() { Interval = PointerInterval };

    /// <summary>
    /// Where the pet looks, on the screen.
    /// </summary>
    private GazeDirection _gaze = GazeDirection.None;

    /// <summary>
    /// Tells where to look in the Idle animation: towards the pointer, as the sprite is drawn.
    /// </summary>
    /// <returns>
    /// The direction in the unmirrored drawing; <see cref="GazeDirection.None"/> unless the pet is idle.
    /// </returns>
    private GazeDirection CurrentLook()
    {
        if (_shownState != PetState.Idle)
        {
            return GazeDirection.None;
        }

        return FlipTransform.ScaleX < 0 ? GazeTracker.Mirror(_gaze) : _gaze;
    }

    /// <summary>
    /// Reads the mouse pointer and turns the pet's eyes towards it.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private void OnPointerTick(object? sender, EventArgs e)
    {
        if (_pressed || !_switches.IsEnabled || !_pet.CanLookAround || _shownState != PetState.Idle)
        {
            SetGaze(GazeDirection.None);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var cursor = MonitorHelper.GetCursorPositionPixels();
        var box = GetPetBox();
        SetGaze(GazeTracker.Of(cursor.X - box.CenterX, cursor.Y - box.CenterY, box.Size / 2));
        ReportTick("pointer", startedAt);
    }

    /// <summary>
    /// Tells where the pet is on the screen, from the window rectangle that is read again only after a move.
    /// </summary>
    /// <returns>The centre and the width of the pet, in device pixels.</returns>
    private PetBox GetPetBox()
    {
        var bounds = _pixelBounds ??= MonitorHelper.GetWindowBoundsPixels(this);
        return new PetBox((bounds.Left + bounds.Right) / 2.0, (bounds.Top + bounds.Bottom) / 2.0, bounds.Width);
    }

    /// <summary>
    /// Turns the eyes, if they are not already looking that way.
    /// </summary>
    /// <param name="gaze">The direction on the screen.</param>
    private void SetGaze(GazeDirection gaze)
    {
        if (gaze == _gaze)
        {
            return;
        }

        _gaze = gaze;
        if (_player.SetState(_shownState, CurrentLook()))
        {
            ShowFrame();
            _animationTimer.Interval = _player.Animation.FrameDuration;
        }
    }

    /// <summary>
    /// Runs the pointer timer only while the pet is on screen, stands around and can look around, so it costs nothing
    /// while the pet walks, sleeps or is busy. Called when the state changes, and every second, as the switches can change
    /// at any time.
    /// </summary>
    private void UpdatePointerTimer()
    {
        var wanted = IsVisible && _switches.IsEnabled && _pet.CanLookAround && _shownState == PetState.Idle;
        if (wanted && !_pointerTimer.IsEnabled)
        {
            _pointerTimer.Start();
        }
        else if (!wanted && _pointerTimer.IsEnabled)
        {
            _pointerTimer.Stop();
            SetGaze(GazeDirection.None);
        }
    }
}
