using DeskBuddy.Domain;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// How far a pet may walk along the ground or the top edge: across a row of monitors that lie side by side.
/// </summary>
/// <param name="Left">
/// The left end of the walkable range, in device pixels of the virtual screen.
/// </param>
/// <param name="Right">
/// The right end of the walkable range, in device pixels of the virtual screen.
/// </param>
/// <param name="Top">
/// The top of the work area of the monitor the pet is on.
/// </param>
/// <param name="Bottom">
/// The bottom of the work area of the monitor the pet is on; the taskbar line.
/// </param>
/// <param name="Current">
/// The whole area of the monitor the pet is on.
/// </param>
/// <param name="Seams">
/// The horizontal positions where one monitor of the row ends and the next one begins.
/// </param>
/// <param name="RightScale">
/// The scaling of the monitor at the right end of the row; a window is as many pixels wide per device-independent pixel
/// there, which is not the scaling it has while it is still on another monitor.
/// </param>
/// <param name="WorkAreas">
/// The work area of every monitor of the row; see <see cref="VerticalAt"/>.
/// </param>
public readonly record struct WalkRange(
    int Left,
    int Right,
    int Top,
    int Bottom,
    PixelRect Current,
    IReadOnlyList<int> Seams,
    double RightScale,
    IReadOnlyList<PixelRect> WorkAreas)
{
    /// <summary>
    /// Works out the row of monitors a pet can walk across from the monitor it is on.
    /// </summary>
    /// <param name="monitors">
    /// Every monitor.
    /// </param>
    /// <param name="centerX">
    /// The horizontal centre of the pet window, in device pixels.
    /// </param>
    /// <param name="centerY">
    /// The vertical centre of the pet window, in device pixels.
    /// </param>
    /// <param name="petHeight">
    /// The height of the pet window, in device pixels.
    /// </param>
    /// <returns>
    /// The range, or <see langword="null"/> if there are no monitors. Another monitor joins the row when it touches the
    /// side of one already in it and has at least the height of the pet in common with it, so the pet is never on a
    /// monitor that is too short to show it. The taskbar line and top edge differ from monitor to monitor; see
    /// <see cref="VerticalAt"/>.
    /// </returns>
    public static WalkRange? Compute(
        IReadOnlyList<MonitorArea> monitors,
        double centerX,
        double centerY,
        int petHeight)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        if (monitors.Count == 0)
        {
            return null;
        }

        var current = monitors[IndexOfMonitor(monitors, centerX, centerY)];

        // The pet is as high in pixels as the scaling of the monitor it is on makes it; the same pet is higher on a monitor
        // with a larger scaling, and has to fit there too.
        var petHeightDip = current.Scale > 0 ? petHeight / current.Scale : petHeight;

        var row = new List<MonitorArea> { current };
        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var candidate in monitors)
            {
                if (!row.Contains(candidate) && row.Any(member => SideBySide(member, candidate, petHeightDip)))
                {
                    row.Add(candidate);
                    grew = true;
                }
            }
        }

        row.Sort((a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
        var seams = row.Skip(1).Select(member => member.Bounds.Left).ToList();

        return new WalkRange(
            row.Min(member => member.WorkArea.Left),
            row.Max(member => member.WorkArea.Right),
            current.WorkArea.Top,
            current.WorkArea.Bottom,
            current.Bounds,
            seams,
            row.MaxBy(member => member.WorkArea.Right).Scale,
            row.Select(member => member.WorkArea).ToList());
    }

    /// <summary>
    /// Works out where the top edge and the taskbar line are for a pet window that lies over some of the monitors of the
    /// row. Where the window lies over several, the pet keeps to the part that is on screen on all of them: below the
    /// lowest top edge and above the highest taskbar line.
    /// </summary>
    /// <param name="workAreas">
    /// The work area of every monitor of the row.
    /// </param>
    /// <param name="left">
    /// The window's left edge, in device pixels.
    /// </param>
    /// <param name="right">
    /// The window's right edge, in device pixels.
    /// </param>
    /// <param name="fallback">
    /// The top and bottom to give if the window lies over none of the work areas.
    /// </param>
    /// <returns>
    /// The top edge and the taskbar line, in device pixels.
    /// </returns>
    public static (int Top, int Bottom) VerticalAt(
        IReadOnlyList<PixelRect> workAreas,
        int left,
        int right,
        (int Top, int Bottom) fallback)
    {
        return TryVerticalAt(workAreas, left, right, out var top, out var bottom) ? (top, bottom) : fallback;
    }

    /// <summary>
    /// Works out the top edge and the taskbar line for a window, if it lies over any of the work areas.
    /// </summary>
    /// <param name="workAreas">
    /// The work area of every monitor of the row.
    /// </param>
    /// <param name="left">
    /// The window's left edge, in device pixels.
    /// </param>
    /// <param name="right">
    /// The window's right edge, in device pixels.
    /// </param>
    /// <param name="top">
    /// Receives the lowest top edge of the work areas the window lies over.
    /// </param>
    /// <param name="bottom">
    /// Receives the highest taskbar line of the work areas the window lies over.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the window lies over at least one work area.
    /// </returns>
    private static bool TryVerticalAt(IReadOnlyList<PixelRect> workAreas, int left, int right, out int top, out int bottom)
    {
        ArgumentNullException.ThrowIfNull(workAreas);

        top = int.MinValue;
        bottom = int.MaxValue;
        var any = false;
        foreach (var area in workAreas)
        {
            if (area.Left < right && left < area.Right)
            {
                top = Math.Max(top, area.Top);
                bottom = Math.Min(bottom, area.Bottom);
                any = true;
            }
        }

        return any;
    }

    /// <summary>
    /// The same as <see cref="VerticalAt"/>, in device-independent pixels of the scaling the window has now: the top edge
    /// and the taskbar line the movement of the pet keeps to.
    /// </summary>
    /// <param name="workAreas">
    /// The work area of every monitor of the row, in device pixels.
    /// </param>
    /// <param name="left">
    /// The window's left edge, in device pixels.
    /// </param>
    /// <param name="right">
    /// The window's right edge, in device pixels.
    /// </param>
    /// <param name="scaleY">
    /// Device pixels per device-independent pixel, vertically.
    /// </param>
    /// <param name="fallback">
    /// The top and bottom, in device-independent pixels, to give if the window lies over none of the work areas.
    /// </param>
    /// <returns>
    /// The top edge and the taskbar line, in device-independent pixels.
    /// </returns>
    public static (double Top, double Bottom) VerticalAtDip(
        IReadOnlyList<PixelRect> workAreas,
        int left,
        int right,
        double scaleY,
        (double Top, double Bottom) fallback)
    {
        return TryVerticalAt(workAreas, left, right, out var top, out var bottom)
            ? (top / scaleY, bottom / scaleY)
            : fallback;
    }

    /// <summary>
    /// Finds the monitor a point is on, or the nearest one if it is on none.
    /// </summary>
    /// <param name="monitors">
    /// Every monitor; not empty.
    /// </param>
    /// <param name="x">
    /// The horizontal position of the point.
    /// </param>
    /// <param name="y">
    /// The vertical position of the point.
    /// </param>
    /// <returns>
    /// The index of the monitor.
    /// </returns>
    private static int IndexOfMonitor(IReadOnlyList<MonitorArea> monitors, double x, double y)
    {
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < monitors.Count; i++)
        {
            var bounds = monitors[i].Bounds;

            // The right and bottom edges belong to the next pixel, so a point on the seam is on the monitor to its right.
            var dx = Math.Max(Math.Max(bounds.Left - x, x - bounds.Right + 1), 0);
            var dy = Math.Max(Math.Max(bounds.Top - y, y - bounds.Bottom + 1), 0);
            var distance = (dx * dx) + (dy * dy);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Tells whether two monitors lie side by side: one's right edge is the other's left edge, and they have at least the
    /// given height in common.
    /// </summary>
    /// <param name="first">
    /// The first monitor.
    /// </param>
    /// <param name="second">
    /// The second monitor.
    /// </param>
    /// <param name="petHeightDip">
    /// The height of the pet in device-independent pixels; the monitors have to share as many pixels as that makes at the
    /// larger of their scalings.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if they touch at a side.
    /// </returns>
    private static bool SideBySide(MonitorArea first, MonitorArea second, double petHeightDip)
    {
        var a = first.Bounds;
        var b = second.Bounds;
        var minHeight = (int)Math.Ceiling(petHeightDip * Math.Max(first.Scale, second.Scale));
        var shared = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
        return shared > 0 && shared >= minHeight && (a.Right == b.Left || b.Right == a.Left);
    }
}

/// <summary>
/// Keeps a pet from standing still on the seam between two monitors.
/// </summary>
public static class SeamGuard
{
    /// <summary>
    /// Tells whether a change of state is to wait because the pet is walking across the seam between two monitors.
    /// </summary>
    /// <param name="shown">
    /// The state the pet shows now.
    /// </param>
    /// <param name="target">
    /// The state it is to change to.
    /// </param>
    /// <param name="onSeam">
    /// Whether the pet lies across a seam.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a walking pet on a seam is to walk on first. Being picked up or falling never waits.
    /// </returns>
    public static bool HoldsStateChange(PetState shown, PetState target, bool onSeam)
    {
        return onSeam && shown.IsWalking() && target != PetState.Walk && target != PetState.Drag && !target.IsFalling();
    }

    /// <summary>
    /// Tells whether a window lies across a seam, with some of it on each side.
    /// </summary>
    /// <param name="left">
    /// The window's left edge, in device pixels.
    /// </param>
    /// <param name="right">
    /// The window's right edge, in device pixels.
    /// </param>
    /// <param name="seams">
    /// The positions of the seams.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a seam runs through the window; a window that only touches it is not across it.
    /// </returns>
    public static bool Straddles(int left, int right, IReadOnlyList<int> seams)
    {
        ArgumentNullException.ThrowIfNull(seams);

        return seams.Any(seam => left < seam && seam < right);
    }

    /// <summary>
    /// Tells whether a window, as it moves a given distance in a direction, would lie across a seam at some point: it is
    /// across one now, or it would reach one on the way.
    /// </summary>
    /// <param name="left">
    /// The window's left edge, in device pixels.
    /// </param>
    /// <param name="right">
    /// The window's right edge, in device pixels.
    /// </param>
    /// <param name="facing">
    /// The direction of the move: <c>-1</c> for left, <c>1</c> for right.
    /// </param>
    /// <param name="travel">
    /// How far the window moves, in device pixels.
    /// </param>
    /// <param name="seams">
    /// The positions of the seams.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a seam lies within the span the window sweeps over; a window that only touches a seam
    /// and moves away from it does not cross it.
    /// </returns>
    public static bool CrossesSeam(int left, int right, int facing, int travel, IReadOnlyList<int> seams)
    {
        ArgumentNullException.ThrowIfNull(seams);

        var sweptLeft = facing < 0 ? left - travel : left;
        var sweptRight = facing < 0 ? right : right + travel;
        return Straddles(sweptLeft, sweptRight, seams);
    }

    /// <summary>
    /// Moves a window that lies across a seam to the side where more of it is.
    /// </summary>
    /// <param name="left">
    /// The window's left edge, in device pixels.
    /// </param>
    /// <param name="right">
    /// The window's right edge, in device pixels.
    /// </param>
    /// <param name="seams">
    /// The positions of the seams.
    /// </param>
    /// <returns>
    /// The window's new left edge; the same one if it is not across a seam. When both sides hold the same part, the window
    /// goes to the left one.
    /// </returns>
    public static int NudgeOffSeam(int left, int right, IReadOnlyList<int> seams)
    {
        ArgumentNullException.ThrowIfNull(seams);

        foreach (var seam in seams)
        {
            if (left < seam && seam < right)
            {
                return seam - left >= right - seam ? seam - (right - left) : seam;
            }
        }

        return left;
    }
}
