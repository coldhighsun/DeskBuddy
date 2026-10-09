namespace DeskBuddy.Domain.Behavior;

/// <summary>
/// Horizontal walking along a line, turning around at the ends.
/// </summary>
public static class WalkMovement
{
    /// <summary>
    /// Moves a position along the line by one time step, bouncing off the ends.
    /// </summary>
    /// <param name="x">
    /// Current position.
    /// </param>
    /// <param name="direction">
    /// Current direction: negative for left, positive for right.
    /// </param>
    /// <param name="speed">
    /// Speed in units per second.
    /// </param>
    /// <param name="delta">
    /// Elapsed time since the previous step.
    /// </param>
    /// <param name="minX">
    /// Left end of the walkable range.
    /// </param>
    /// <param name="maxX">
    /// Right end of the walkable range. If smaller than <paramref name="minX"/>, the range collapses to
    /// <paramref name="minX"/>.
    /// </param>
    /// <returns>
    /// The new position (always within the range) and the new direction (<c>-1</c> or <c>1</c>).
    /// </returns>
    public static (double X, int Direction) Step(
        double x,
        int direction,
        double speed,
        TimeSpan delta,
        double minX,
        double maxX)
    {
        maxX = Math.Max(minX, maxX);
        var heading = direction < 0 ? -1 : 1;
        var next = x + (heading * speed * delta.TotalSeconds);

        if (next <= minX)
        {
            return (minX, 1);
        }

        if (next >= maxX)
        {
            return (maxX, -1);
        }

        return (next, heading);
    }
}
