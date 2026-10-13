namespace DeskBuddy.App.Windows;

/// <summary>
/// The tabs of the panel window, in the order they appear.
/// </summary>
public enum PanelTab
{
    /// <summary>
    /// Skin, size, language and movement mode.
    /// </summary>
    Appearance = 0,

    /// <summary>
    /// Timings, full-screen behavior, development detection and scenarios.
    /// </summary>
    Behavior = 1,

    /// <summary>
    /// Boss key, start with Windows and diagnostics.
    /// </summary>
    System = 2,
}
