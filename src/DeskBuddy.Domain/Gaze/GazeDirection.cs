namespace DeskBuddy.Domain.Gaze;

/// <summary>
/// Where the pet looks while it stands around.
/// </summary>
public enum GazeDirection
{
    /// <summary>
    /// Straight ahead.
    /// </summary>
    None,

    /// <summary>
    /// To the left of the screen.
    /// </summary>
    Left,

    /// <summary>
    /// To the right of the screen.
    /// </summary>
    Right,

    /// <summary>
    /// Up.
    /// </summary>
    Up,

    /// <summary>
    /// Down.
    /// </summary>
    Down,
}
