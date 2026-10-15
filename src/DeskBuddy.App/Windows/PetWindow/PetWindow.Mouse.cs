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

        _pressed = false;
        ReleaseMouseCapture();

        if (_dragging)
        {
            _dragging = false;
            _brain.OnDragEnded();
            if (!TryCrawlOnEdge() && !TryHangOnEdge() && !TryStartFall())
            {
                SettleInWorkArea();
            }
        }
        else
        {
            _brain.OnClicked();
        }

        ApplyState(_brain.State);
    }
}
