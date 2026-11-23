using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// The scaling the pictures of the pet and of the previews have to be sharp at: that of the monitor with the largest
/// scaling that is connected, as the pet window may be dragged onto any of them.
/// </summary>
internal static class DisplayScale
{
    /// <summary>
    /// The smallest scaling used (1 is 100 %), whatever the monitors report.
    /// </summary>
    public const double Minimum = 1;

    /// <summary>
    /// The largest scaling used (3 is 300 %), which is as high as Windows offers.
    /// </summary>
    public const double Maximum = 3;

    /// <summary>
    /// Keeps a scaling inside the range that is used.
    /// </summary>
    /// <param name="scale">The scaling.</param>
    /// <returns>The scaling, at least <see cref="Minimum"/> and at most <see cref="Maximum"/>; <see cref="Minimum"/> for a value that is no number.</returns>
    public static double Clamp(double scale)
    {
        return double.IsNaN(scale) ? Minimum : Math.Clamp(scale, Minimum, Maximum);
    }

    /// <summary>
    /// Picks the largest of the scalings of a set of monitors.
    /// </summary>
    /// <param name="scales">The scalings of the monitors.</param>
    /// <returns>The largest, kept inside the range that is used; <see cref="Minimum"/> if there are none.</returns>
    public static double Largest(IEnumerable<double> scales)
    {
        ArgumentNullException.ThrowIfNull(scales);

        var largest = Minimum;
        foreach (var scale in scales)
        {
            largest = Math.Max(largest, Clamp(scale));
        }

        return largest;
    }

    /// <summary>
    /// Reads the largest scaling of the monitors that are connected now.
    /// </summary>
    /// <returns>The scaling, kept inside the range that is used; <see cref="Minimum"/> if the monitors cannot be listed.</returns>
    public static double Current()
    {
        return Largest(MonitorHelper.GetAllMonitors().Select(monitor => monitor.Scale));
    }
}
