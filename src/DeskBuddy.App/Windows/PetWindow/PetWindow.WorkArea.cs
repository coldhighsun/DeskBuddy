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
/// Tracking the work area, its monitor and the seams between monitors.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// Number of movement ticks between refreshes of the cached work area (about one second).
    /// </summary>
    private const int WorkAreaRefreshEveryTicks = 30;

    /// <summary>
    /// The range the pet may take, cached while walking: the work area of the monitor it is on, or, when it walks along
    /// the taskbar, the whole row of monitors lying side by side (the taskbar line and top edge are those of the monitor
    /// it is on).
    /// </summary>
    private Rect _workArea;

    /// <summary>
    /// The positions, in device pixels, where the monitors the pet may walk across meet; empty when it is limited to one
    /// monitor.
    /// </summary>
    private IReadOnlyList<int> _seams = [];

    /// <summary>
    /// The whole area, in device pixels, of the monitor the cached work area was worked out for.
    /// </summary>
    private PixelRect _workAreaMonitor;

    /// <summary>
    /// The work area, in device pixels, of every monitor the pet may walk across; that of its own monitor only when it is
    /// limited to one. The top edge and taskbar line the pet keeps to come from these (see
    /// <see cref="CreateMovementContext"/>); the vertical extent of <see cref="_workArea"/> is only the fallback.
    /// </summary>
    private IReadOnlyList<PixelRect> _rowAreas = [];

    /// <summary>
    /// Movement ticks since the work area was last refreshed; reset when a walk starts.
    /// </summary>
    private int _moveTicksSinceWorkAreaRefresh;

    /// <inheritdoc />
    bool IScreenEdges.HasMonitorBeside(bool left)
    {
        return MonitorHelper.HasMonitorBeside(this, left);
    }

    /// <inheritdoc />
    bool IScreenEdges.HasMonitorAbove()
    {
        return MonitorHelper.HasMonitorAbove(this);
    }

    /// <summary>
    /// The work area only changes when the taskbar or monitors change, so refresh it about once per second instead of
    /// on every movement tick.
    /// </summary>
    private void RefreshWorkAreaPeriodically()
    {
        if (_moveTicksSinceWorkAreaRefresh++ % WorkAreaRefreshEveryTicks == 0 || HasLeftWorkAreaMonitor())
        {
            RefreshWorkArea();
        }
    }

    /// <summary>
    /// Works out the range the pet may take: the whole row of monitors lying side by side with the one it is on, so it
    /// can walk from one to the next. The taskbar line and the top edge are those of the monitor the pet is on here;
    /// <see cref="CreateMovementContext"/> takes the other monitors into account while the pet lies over several.
    /// </summary>
    private void RefreshWorkArea()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var range = MonitorHelper.GetWalkRangePixels(this);

        // With several monitors the pet is as wide in pixels at the far end as the scaling of the monitor there makes it,
        // not as it is on the monitor it is on now; the end is moved so that the pet still fits when it gets there.
        var right = range.Seams.Count > 0
            ? range.Right + (Width * (dpi.DpiScaleX - range.RightScale))
            : range.Right;
        _workArea = new Rect(
            range.Left / dpi.DpiScaleX,
            range.Top / dpi.DpiScaleY,
            (right - range.Left) / dpi.DpiScaleX,
            (range.Bottom - range.Top) / dpi.DpiScaleY);
        _seams = range.Seams;
        _rowAreas = range.WorkAreas;
        _workAreaMonitor = range.Current;
    }

    /// <summary>
    /// Tells whether the pet has moved on to another monitor than the one the cached work area was worked out for, which
    /// changes its taskbar line and scaling, so the work area is to be refreshed at once.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the centre of the pet is no longer on that monitor.
    /// </returns>
    private bool HasLeftWorkAreaMonitor()
    {
        if (_seams.Count == 0 || _workAreaMonitor.Width <= 0)
        {
            return false;
        }

        var bounds = WindowPixels();
        var center = bounds.Left + (bounds.Width / 2);
        return center < _workAreaMonitor.Left || center >= _workAreaMonitor.Right;
    }

    /// <summary>
    /// Tells whether the pet lies across the seam between two monitors, with part of it on each.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if it does; the pet then has to walk on until it is on one monitor.
    /// </returns>
    private bool IsOnSeam()
    {
        if (_seams.Count == 0)
        {
            return false;
        }

        // The real position of the window: Window.Left can be out by the ratio of two scalings after the pet has walked
        // onto another monitor (see CurrentPosition).
        var bounds = WindowPixels();
        return SeamGuard.Straddles(bounds.Left, bounds.Right, _seams);
    }

    /// <summary>
    /// Moves a pet that lies across the seam between two monitors onto the one where more of it is.
    /// </summary>
    private void NudgeOffSeam()
    {
        if (_seams.Count == 0)
        {
            return;
        }

        var bounds = MonitorHelper.GetWindowBoundsPixels(this);
        var left = SeamGuard.NudgeOffSeam(bounds.Left, bounds.Right, _seams);
        if (left != bounds.Left)
        {
            WindowMover.MoveToPixels(this, left, bounds.Top);
        }
    }
}
