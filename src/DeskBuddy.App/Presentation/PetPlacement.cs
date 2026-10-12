namespace DeskBuddy.App.Presentation;

/// <summary>
/// Where a new pet appears.
/// </summary>
/// <param name="X">
/// The left edge of the pet window in device pixels of the virtual screen.
/// </param>
/// <param name="Y">
/// The top edge of the pet window in device pixels of the virtual screen.
/// </param>
/// <param name="Scale">
/// The scaling of the monitor the spot is on: device pixels per device-independent pixel.
/// </param>
public readonly record struct PetSpot(int X, int Y, double Scale)
{
    /// <summary>
    /// Works out the value of <see cref="System.Windows.Window.Left"/> that puts a window on the left edge of the spot. A
    /// window turns its position into device pixels with the scaling of the monitor it is on at that moment, which is not
    /// necessarily the monitor of the spot, so it is that scaling that is divided by, not <see cref="Scale"/>.
    /// </summary>
    /// <param name="windowScale">
    /// The scaling of the window now: device pixels per device-independent pixel.
    /// </param>
    /// <returns>
    /// The left edge in device-independent pixels.
    /// </returns>
    public double LeftFor(double windowScale)
    {
        return X / windowScale;
    }

    /// <summary>
    /// Works out the value of <see cref="System.Windows.Window.Top"/> that puts a window on the top edge of the spot; see
    /// <see cref="LeftFor"/>.
    /// </summary>
    /// <param name="windowScale">
    /// The scaling of the window now: device pixels per device-independent pixel.
    /// </param>
    /// <returns>
    /// The top edge in device-independent pixels.
    /// </returns>
    public double TopFor(double windowScale)
    {
        return Y / windowScale;
    }
}

/// <summary>
/// Decides where a new pet appears on the screen.
/// </summary>
public static class PetPlacement
{
    /// <summary>
    /// Picks a random spot on the bottom edge of the work area of a random monitor, with the pet entirely inside the area
    /// and at least a margin away from its left and right edges. The pet is measured in device-independent pixels and
    /// converted with the scaling of the monitor it lands on, so the spot is right after the window has moved there. A
    /// monitor with room for the pet between the margins is preferred.
    /// </summary>
    /// <param name="monitors">
    /// The monitors; at least one.
    /// </param>
    /// <param name="widthDip">
    /// The width of the pet window in device-independent pixels.
    /// </param>
    /// <param name="heightDip">
    /// The height of the pet window in device-independent pixels.
    /// </param>
    /// <param name="marginDip">
    /// How far from the left and right edges of the area the pet starts, in device-independent pixels, so that a new pet
    /// is not taken for one dropped at the edge.
    /// </param>
    /// <param name="random">
    /// The source of chance.
    /// </param>
    /// <returns>
    /// The top-left corner of the pet window in device pixels, with the scaling of the monitor it is on. If no monitor has
    /// room for the pet between the margins,
    /// the pet is centered, as far from the edges as the area allows; if the area is smaller than the pet, its top-left
    /// corner wins.
    /// </returns>
    public static PetSpot PickOnGround(
        IReadOnlyList<MonitorArea> monitors,
        double widthDip,
        double heightDip,
        double marginDip,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfZero(monitors.Count);

        var roomy = monitors.Where(monitor => HasRoom(monitor, widthDip, marginDip)).ToList();
        var pool = roomy.Count > 0 ? roomy : monitors;
        var chosen = pool[random.Next(pool.Count)];

        var area = chosen.WorkArea;
        var width = ToPixels(widthDip, chosen.Scale);
        var (low, high) = StartRange(chosen, widthDip, marginDip);
        var x = high > low ? random.Next(low, high + 1) : Math.Max(area.Left, ((area.Left + area.Right) - width) / 2);
        var y = Math.Max(area.Top, area.Bottom - ToPixels(heightDip, chosen.Scale));
        return new PetSpot(x, y, chosen.Scale);
    }

    /// <summary>
    /// Puts a window on a spot. A shown window is moved in device pixels, both coordinates in one step. A window that is
    /// not shown, because it does not exist yet or is hidden, is placed through its Left and Top, which are turned into
    /// device pixels with the window's own scaling; the scaling is read again for Top, since setting Left can take the window
    /// to a monitor of another scaling.
    /// </summary>
    /// <remarks>
    /// Assumes that a window turns Left and Top into device pixels with the scaling it reports. That was only run on a
    /// machine with one monitor, where it makes no difference; on monitors of different scaling it still needs checking.
    /// </remarks>
    /// <param name="spot">
    /// The spot.
    /// </param>
    /// <param name="target">
    /// The window to place.
    /// </param>
    public static void Place(PetSpot spot, IPlacementTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsVisible)
        {
            target.MoveToPixels(spot.X, spot.Y);
            return;
        }

        target.Left = spot.LeftFor(target.ScaleX);
        target.Top = spot.TopFor(target.ScaleY);
    }

    /// <summary>
    /// Works out the range of left edges that keep the pet inside the work area of a monitor, a margin away from both
    /// sides.
    /// </summary>
    /// <param name="monitor">
    /// The monitor.
    /// </param>
    /// <param name="widthDip">
    /// The width of the pet window in device-independent pixels.
    /// </param>
    /// <param name="marginDip">
    /// The margin in device-independent pixels.
    /// </param>
    /// <returns>
    /// The smallest and the largest left edge, in device pixels; the range is empty if the largest is not greater than the
    /// smallest.
    /// </returns>
    private static (int Low, int High) StartRange(MonitorArea monitor, double widthDip, double marginDip)
    {
        var margin = ToPixels(marginDip, monitor.Scale);
        return (monitor.WorkArea.Left + margin, monitor.WorkArea.Right - ToPixels(widthDip, monitor.Scale) - margin);
    }

    /// <summary>
    /// Tells whether the pet fits into the work area of a monitor with a margin on both sides.
    /// </summary>
    /// <param name="monitor">
    /// The monitor.
    /// </param>
    /// <param name="widthDip">
    /// The width of the pet window in device-independent pixels.
    /// </param>
    /// <param name="marginDip">
    /// The margin in device-independent pixels.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if there is room.
    /// </returns>
    private static bool HasRoom(MonitorArea monitor, double widthDip, double marginDip)
    {
        var (low, high) = StartRange(monitor, widthDip, marginDip);
        return high > low;
    }

    /// <summary>
    /// Converts a length to device pixels, rounding up so the window never is smaller than it is.
    /// </summary>
    /// <param name="dip">
    /// The length in device-independent pixels.
    /// </param>
    /// <param name="scale">
    /// Device pixels per device-independent pixel.
    /// </param>
    /// <returns>
    /// The length in device pixels.
    /// </returns>
    private static int ToPixels(double dip, double scale)
    {
        return (int)Math.Ceiling(dip * scale);
    }
}
