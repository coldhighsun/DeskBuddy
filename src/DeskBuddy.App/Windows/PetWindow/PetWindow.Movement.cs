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
/// Walking and hopping along the taskbar line.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// Picks a random spot on the bottom edge of the work area of a monitor picked at random (above the taskbar), away
    /// from the left and right edges. The spot is worked out with the scaling of that monitor, so it is right once the
    /// window is there.
    /// </summary>
    /// <returns>
    /// The spot, in device pixels, with the scaling of the monitor it is on.
    /// </returns>
    private PetSpot PickRandomSpot()
    {
        var monitors = MonitorHelper.GetAllMonitors();
        if (monitors.Count == 0)
        {
            monitors = [new MonitorArea(MonitorHelper.GetWorkAreaPixels(this), VisualTreeHelper.GetDpi(this).DpiScaleX)];
        }

        return PetPlacement.PickOnGround(monitors, Width, Height, StartMargin, Random.Shared);
    }

    /// <summary>
    /// Describes where the pet is and where it may go, for the movement strategy.
    /// </summary>
    /// <returns>
    /// The context, using the cached work area.
    /// </returns>
    private MovementContext CreateMovementContext()
    {
        // The top edge and the taskbar line of monitors side by side need not be at the same height. A pet that lies over
        // two of them keeps to what is on screen on both, so it is never partly out of sight; it takes the line of the
        // monitor it is on once it has crossed over.
        var window = WindowPixels();
        var (top, bottom) = WalkRange.VerticalAtDip(
            _rowAreas,
            window.Left,
            window.Right,
            VisualTreeHelper.GetDpi(this).DpiScaleY,
            (_workArea.Top, _workArea.Bottom));

        var bounds = new MovementBounds(
            _workArea.Left,
            _workArea.Right - Width,
            top,
            bottom - Height);
        return new MovementContext(
            CurrentPosition(),
            new Point2(Width, Height),
            bounds,
            _shownState == PetState.Hop ? _options.WalkSpeed * _options.HopSpeedFactor : _options.WalkSpeed);
    }

    /// <summary>
    /// Gets where the pet window really is, in device-independent pixels of its present scaling. <see cref="Window.Left"/>
    /// is not used for it: it is kept in device-independent pixels of the scaling the window had when it was last moved,
    /// so after the pet walks onto a monitor with another scaling it can be out by the ratio of the two scalings, which
    /// made the pet leap ahead. The position the walk step worked out last is returned as long as the window has not been
    /// moved by anything else, so that steps of less than a pixel are not lost.
    /// </summary>
    /// <returns>
    /// The position of the window's top-left corner, on the line the pet walks along: a hop that has raised the window is
    /// not part of it.
    /// </returns>
    private Point2 CurrentPosition()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return _hop.ToWalkingLine(_stepTracker.Resolve(WindowPixels(), dpi.DpiScaleX, dpi.DpiScaleY));
    }

    /// <summary>
    /// Gets the real bounds of the pet window in device pixels: those read earlier in the movement tick that is running,
    /// otherwise read now.
    /// </summary>
    /// <returns>
    /// The bounds of the window.
    /// </returns>
    private PixelRect WindowPixels()
    {
        return _tickBounds ?? MonitorHelper.GetWindowBoundsPixels(this);
    }

    /// <summary>
    /// Moves the pet window to the position a walk or crawl step worked out, and remembers it as the pet's position.
    /// </summary>
    /// <param name="position">
    /// The wanted position of the window's top-left corner, in device-independent pixels of the present scaling.
    /// </param>
    private void MoveToStepPosition(Point2 position)
    {
        var before = VisualTreeHelper.GetDpi(this);
        var pixels = StepTracker.ToPixels(position, before.DpiScaleX, before.DpiScaleY);

        // Moved only if it is a different device pixel than the one the window is at.
        var actual = WindowPixels();
        if (actual.Left != pixels.X || actual.Top != pixels.Y)
        {
            WindowMover.MoveToPixelsSettled(this, pixels.X, pixels.Y);
            _tickBounds = null;
        }

        // The scaling may have changed with the move; the exact position is then the pixels in the new scaling.
        var after = VisualTreeHelper.GetDpi(this);
        if (after.DpiScaleX != before.DpiScaleX || after.DpiScaleY != before.DpiScaleY)
        {
            position = new Point2(pixels.X / after.DpiScaleX, pixels.Y / after.DpiScaleY);
        }

        _stepTracker.Remember(pixels, position, after.DpiScaleX, after.DpiScaleY);
    }

    /// <summary>
    /// Lets the movement strategy prepare a new walk and turns the sprite the way it will go.
    /// </summary>
    private void BeginWalk()
    {
        RefreshWorkArea();
        _moveTicksSinceWorkAreaRefresh = 0;
        _facing = _movement.BeginWalk(CreateMovementContext(), _brain.WalkDirection);
        ApplyFacing();
    }

    /// <summary>
    /// Tells whether the pet has room for a hop where it is, in the direction it walks: the hop must not end against the
    /// edge of the screen, and must not carry the pet over the seam between two monitors. Asked by the brain when a hop
    /// is about to start.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the hop fits before the edge and does not cross a seam.
    /// </returns>
    private bool CanHopHere()
    {
        if (_brain.HopDuration is not { } hopDuration)
        {
            return false;
        }

        var context = CreateMovementContext();
        var room = HopRoom.Required(_options.WalkSpeed, _options.HopSpeedFactor, hopDuration);
        if (!HopRoom.IsClear(context.Position, _facing, context.Bounds, room))
        {
            return false;
        }

        // A hop never carries the pet over the seam between two monitors, so it cannot come down on another taskbar line.
        var bounds = WindowPixels();
        var travel = (int)Math.Ceiling(room * VisualTreeHelper.GetDpi(this).DpiScaleX);
        return !SeamGuard.CrossesSeam(bounds.Left, bounds.Right, _facing, travel, _seams);
    }

    /// <summary>
    /// Puts the pet back on the line it walks along if a hop has raised it, so it is not left in mid-air. Whatever takes
    /// the pet away from walking (crawling, falling, being picked up) calls it before it reads the pet's position.
    /// </summary>
    private void LandFromHop()
    {
        if (!_hop.IsLifted)
        {
            return;
        }

        var landed = CurrentPosition();
        _hop.Reset();
        MoveToStepPosition(landed);
    }

    /// <summary>
    /// Moves the pet one step along the taskbar line.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnMoveTick(object? sender, EventArgs e)
    {
        // The window bounds are read once and shared by everything in the tick, until the window is moved.
        _tickBounds = null;
        try
        {
            MoveOnce();
        }
        finally
        {
            _tickBounds = null;
        }
    }

    /// <summary>
    /// Moves the pet one step, whatever it is doing: falling, crawling or walking.
    /// </summary>
    private void MoveOnce()
    {
        var delta = _moveWatch.Elapsed;
        _moveWatch.Restart();

        _tickBounds = MonitorHelper.GetWindowBoundsPixels(this);

        if (_shownState.IsFalling())
        {
            OnFallTick(delta);
            return;
        }

        if (IsCrawling)
        {
            OnCrawlTick(delta);
            return;
        }

        RefreshWorkAreaPeriodically();

        var context = CreateMovementContext();
        var step = _movement.Step(context, delta);

        // Monitors side by side can have their taskbar lines at different heights. Onto a lower line the pet falls (under
        // its umbrella if it is high enough above it); onto a higher one it climbs up quickly, instead of jumping.
        var position = step.Position;
        if (_rowAreas.Count > 1)
        {
            var change = VerticalEase.Classify(context.Position.Y, position.Y, _options.VerticalEaseTolerance);
            if (change == VerticalChange.Drop && TryStartFall(refreshWorkArea: false))
            {
                ApplyState(_brain.State);
                return;
            }

            if (change != VerticalChange.None)
            {
                position = position with
                {
                    Y = VerticalEase.Approach(
                        context.Position.Y,
                        position.Y,
                        _options.VerticalEaseSpeed * delta.TotalSeconds),
                };
            }
        }

        // A hop lifts the window above the line it walks along, but never off the top of the screen.
        position = _hop.Lift(
            position,
            _shownState == PetState.Hop ? _hopWatch.Elapsed : null,
            _brain.HopDuration ?? TimeSpan.Zero,
            Height,
            _options.HopHeightRatio,
            context.Bounds.MinY);

        // One cheap move for both coordinates, and none at all when the step stays within the same device pixel.
        MoveToStepPosition(position);

        if (step.Facing != _facing)
        {
            _facing = step.Facing;
            ApplyFacing();
        }

        // A hop is played out to its end, then the walk goes on.
        if (_shownState == PetState.Hop && HopArc.IsOver(_hopWatch.Elapsed, _brain.HopDuration ?? TimeSpan.Zero))
        {
            _brain.OnHopEnded();
            ApplyState(_brain.State);
        }

        // The pet is off the seam: show what the brain wanted while it was on it, if that still holds (a short reaction
        // that ran out meanwhile is gone).
        if (_stateHeld && !IsOnSeam())
        {
            _stateHeld = false;
            ApplyState(_brain.State);
            return;
        }

        if (TryStartCrawlAtEnd(step.Position, context.Bounds))
        {
            ApplyState(_brain.State);
        }
    }
}
