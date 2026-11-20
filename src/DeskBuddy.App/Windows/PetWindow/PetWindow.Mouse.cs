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
/// Mouse presses, clicks and dragging.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// Distance, in device-independent pixels, the mouse must travel with the button held before a press becomes a
    /// drag instead of a click.
    /// </summary>
    private const double DragThreshold = 4;

    /// <summary>
    /// Whether the left mouse button is currently held on the pet.
    /// </summary>
    private bool _pressed;

    /// <summary>
    /// Whether the current press has turned into a drag.
    /// </summary>
    private bool _dragging;

    /// <summary>
    /// Cursor position, in device pixels, where the current press started.
    /// </summary>
    private (int X, int Y) _pressPixel;

    /// <summary>
    /// Keeps the grab point under the cursor while dragging, in device pixels so it holds across monitors of different
    /// scaling.
    /// </summary>
    private readonly DragTracker _dragTracker = new();

    /// <summary>
    /// The clock the pointer positions of a drag are timed with, for the speed of a throw.
    /// </summary>
    private readonly Stopwatch _dragWatch = Stopwatch.StartNew();

    /// <summary>
    /// Starts tracking a press on the pet; it becomes a click or a drag depending on how the mouse moves.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Mouse event data.
    /// </param>
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            _openPanel(null);
            return;
        }

        _pressed = true;
        _dragging = false;
        _pressPixel = MonitorHelper.GetCursorPositionPixels();
        _dragTracker.Begin(_pressPixel.X, _pressPixel.Y, MonitorHelper.GetWindowBoundsPixels(this));
        _throw.Reset();
        CaptureMouse();
    }

    /// <summary>
    /// Turns a press into a drag once the mouse has moved far enough, and moves the pet with the cursor.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Mouse event data.
    /// </param>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        var cursor = MonitorHelper.GetCursorPositionPixels();
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var moved = Math.Sqrt(Math.Pow(cursor.X - _pressPixel.X, 2) + Math.Pow(cursor.Y - _pressPixel.Y, 2));
        if (!_dragging && moved >= DragThreshold * scale)
        {
            _dragging = true;
            LandFromHop();
            _brain.OnDragStarted();
            ApplyState(_brain.State);
        }

        if (_dragging)
        {
            // In device-independent pixels, the unit the fall works in, so a throw is as strong on every monitor.
            _throw.Add(_dragWatch.Elapsed, cursor.X / scale, cursor.Y / scale);
            var bounds = MonitorHelper.GetWindowBoundsPixels(this);
            var (x, y) = _dragTracker.Move(cursor.X, cursor.Y, bounds.Width, bounds.Height);
            WindowMover.MoveToPixels(this, x, y);
        }
    }

    /// <summary>
    /// Ends a press: a drop settles the pet on the taskbar line, a short press counts as a click.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Mouse event data.
    /// </param>
    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        EndPress(clicked: true);
    }

    /// <summary>
    /// Ends a press that was cut short because the window lost the mouse capture (for example the user switched
    /// windows, a system dialog opened or the pet was hidden). A drag ends as if the pet had been dropped where it is;
    /// a press that had not become a drag is forgotten rather than counted as a click.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Mouse event data.
    /// </param>
    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        EndPress(clicked: false);
    }

    /// <summary>
    /// Finishes the current press: a drag drops the pet, otherwise the press counts as a click if asked.
    /// </summary>
    /// <param name="clicked">
    /// Whether the press ended with the button let go: a press that did not become a drag then counts as a click, and a
    /// drag may throw the pet.
    /// </param>
    private void EndPress(bool clicked)
    {
        // Clear the flag first: releasing the capture raises LostMouseCapture, which must find nothing to end.
        _pressed = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (_dragging)
        {
            _dragging = false;

            // Only a real release can throw; a drag cut short just drops the pet.
            var thrown = clicked ? _throw.GetVelocity(_dragWatch.Elapsed) : default;
            _brain.OnDragEnded();
            if (thrown != default)
            {
                // A thrown pet flies; it does not grab the edge it was let go next to.
                if (!TryStartFall(throwVelocity: thrown))
                {
                    SettleInWorkArea();
                }
            }
            else if (!TryCrawlOnEdge() && !TryStartFall())
            {
                SettleInWorkArea();
            }
        }
        else if (clicked)
        {
            _brain.OnClicked();
        }

        ApplyState(_brain.State);
    }
}
