namespace DeskBuddy.Domain.Gaze;

/// <summary>
/// Works out where the pet should look to follow the mouse pointer.
/// </summary>
public static class GazeTracker
{
    /// <summary>
    /// Tells the direction from the pet to the pointer.
    /// </summary>
    /// <param name="deltaX">
    /// How far the pointer is to the right of the pet centre (negative: to the left), in any unit.
    /// </param>
    /// <param name="deltaY">
    /// How far the pointer is below the pet centre (negative: above), in the same unit.
    /// </param>
    /// <param name="deadZone">
    /// The distance, in the same unit, within which the pointer counts as right in front of the pet.
    /// </param>
    /// <returns>
    /// The direction of the larger of the two offsets, or <see cref="GazeDirection.None"/> inside the dead zone.
    /// </returns>
    public static GazeDirection Of(double deltaX, double deltaY, double deadZone)
    {
        if ((deltaX * deltaX) + (deltaY * deltaY) <= deadZone * deadZone)
        {
            return GazeDirection.None;
        }

        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            return deltaX < 0 ? GazeDirection.Left : GazeDirection.Right;
        }

        return deltaY < 0 ? GazeDirection.Up : GazeDirection.Down;
    }

    /// <summary>
    /// Turns a direction around left to right, for a sprite that is drawn mirrored.
    /// </summary>
    /// <param name="direction">
    /// The direction on the screen.
    /// </param>
    /// <returns>
    /// The direction to look for in the unmirrored drawing.
    /// </returns>
    public static GazeDirection Mirror(GazeDirection direction)
    {
        return direction switch
        {
            GazeDirection.Left => GazeDirection.Right,
            GazeDirection.Right => GazeDirection.Left,
            _ => direction,
        };
    }
}
