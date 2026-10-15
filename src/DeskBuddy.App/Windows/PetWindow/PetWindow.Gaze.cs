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
    /// How often the mouse pointer is looked at, for shaking and for following it with the eyes.
    /// </summary>
    private static readonly TimeSpan PointerInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Timer that reads the mouse pointer for shaking and gaze; runs only while the pet is on screen.
    /// </summary>
    private readonly DispatcherTimer _pointerTimer = new() { Interval = PointerInterval };

    /// <summary>
    /// Decides from the pointer whether the pet gets dizzy or turns its eyes.
    /// </summary>
    private readonly PointerWatcher _pointer = new(new MouseShakeDetector());

    /// <summary>
    /// Runs for as long as the pointer is watched; gives the shake detector its time.
    /// </summary>
    private readonly Stopwatch _pointerWatch = Stopwatch.StartNew();

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
    /// Reads the mouse pointer: a wild shake makes the pet dizzy, and while it stands around its eyes follow the
    /// pointer.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnPointerTick(object? sender, EventArgs e)
    {
        if (_pressed || !_switches.IsEnabled)
        {
            SetGaze(GazeDirection.None);
            return;
        }

        var cursor = MonitorHelper.GetCursorPositionPixels();
        var reaction = _pointer.Read(
            _pointerWatch.Elapsed,
            cursor.X,
            cursor.Y,
            _switches.Allows(PetState.Dizzy) && _brain.State != PetState.Drag && !_brain.State.IsFalling(),
            _pet.CanLookAround && _shownState == PetState.Idle,
            _petBox);
        if (reaction.BecomeDizzy)
        {
            _brain.ActOut(PetState.Dizzy, DizzyDuration);
            ApplyState(_brain.State);
        }
        else if (reaction.Gaze is { } gaze)
        {
            SetGaze(gaze);
        }
    }

    /// <summary>
    /// Tells where the pet is on the screen, from the window rectangle that is read again only after a move.
    /// </summary>
    /// <returns>
    /// The centre and the width of the pet, in device pixels.
    /// </returns>
    private PetBox GetPetBox()
    {
        var bounds = _pixelBounds ??= MonitorHelper.GetWindowBoundsPixels(this);
        return new PetBox((bounds.Left + bounds.Right) / 2.0, (bounds.Top + bounds.Bottom) / 2.0, bounds.Width);
    }

    /// <summary>
    /// Turns the eyes, if they are not already looking that way.
    /// </summary>
    /// <param name="gaze">
    /// The direction on the screen.
    /// </param>
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
    /// Runs the pointer timer only while the pet is on screen and something needs the pointer: shaking can make it
    /// dizzy, or its pet can look around. Checked every second, as the switches can change at any time.
    /// </summary>
    private void UpdatePointerTimer()
    {
        var wanted = IsVisible && _switches.IsEnabled && (_switches.Allows(PetState.Dizzy) || _pet.CanLookAround);
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
