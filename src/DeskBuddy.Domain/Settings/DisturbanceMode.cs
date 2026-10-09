namespace DeskBuddy.Domain.Settings;

/// <summary>
/// How much room the pet takes up on the desktop.
/// </summary>
public enum DisturbanceMode
{
    /// <summary>
    /// The pet stays on top of the taskbar and only walks along it.
    /// </summary>
    Docked,

    /// <summary>
    /// The pet roams freely over the desktop and interacts more.
    /// </summary>
    Roam,
}
