namespace DeskBuddy.App.Windows;

/// <summary>
/// The tabs of the panel window, in the order they appear.
/// </summary>
public enum PanelTab
{
    /// <summary>
    /// The installed pets with previews, to pick one from.
    /// </summary>
    Pets = 0,

    /// <summary>
    /// Size, language and movement mode.
    /// </summary>
    Appearance = 1,

    /// <summary>
    /// Timings, full-screen behavior, development detection and scenarios.
    /// </summary>
    Behavior = 2,

    /// <summary>
    /// Boss key, start with Windows and diagnostics.
    /// </summary>
    System = 3,
}
