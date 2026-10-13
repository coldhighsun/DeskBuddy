namespace DeskBuddy.App.Presentation;

/// <summary>
/// The work area of a monitor together with its scaling.
/// </summary>
/// <param name="WorkArea">
/// The screen minus the taskbar, in device pixels of the virtual screen.
/// </param>
/// <param name="Scale">
/// How many device pixels make a device-independent pixel on this monitor: <c>1</c> at 100%, <c>2</c> at 200%.
/// </param>
/// <param name="FullBounds">
/// The whole monitor including the taskbar, in device pixels of the virtual screen; empty to take the work area for it.
/// Two monitors lie side by side when their full bounds touch, which the work areas may not show because of the taskbar.
/// </param>
public readonly record struct MonitorArea(PixelRect WorkArea, double Scale, PixelRect FullBounds = default)
{
    /// <summary>
    /// Gets the whole monitor including the taskbar; the work area if the full bounds are not known.
    /// </summary>
    public PixelRect Bounds => FullBounds.Width > 0 && FullBounds.Height > 0 ? FullBounds : WorkArea;
}
