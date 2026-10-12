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
public readonly record struct MonitorArea(PixelRect WorkArea, double Scale);
