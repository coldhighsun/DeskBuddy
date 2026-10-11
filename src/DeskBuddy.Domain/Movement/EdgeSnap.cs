namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Where a pet that clings to the edge of the screen ends up.
/// </summary>
/// <param name="Position">
/// The top-left corner of the pet window.
/// </param>
/// <param name="Facing">
/// <c>1</c> if the pet clings to the left edge and keeps its drawing, <c>-1</c> for the right edge, where the drawing
/// is mirrored.
/// </param>
public readonly record struct EdgeSnapResult(Point2 Position, int Facing);

/// <summary>
/// Decides whether a pet that was dropped clings to the left or right edge of the work area.
/// </summary>
public static class EdgeSnap
{
    /// <summary>
    /// Finds the edge a dropped pet clings to.
    /// </summary>
    /// <param name="position">
    /// The top-left corner of the pet window where it was dropped.
    /// </param>
    /// <param name="size">
    /// The size of the pet window.
    /// </param>
    /// <param name="area">
    /// The work area: the range its top-left corner may take, with the largest values already reduced by the window
    /// size (see <see cref="MovementBounds"/>).
    /// </param>
    /// <param name="snapDistance">
    /// How close to an edge the pet must be dropped.
    /// </param>
    /// <param name="allowLeft">
    /// Whether the left edge of the area is an edge of the screen; <see langword="false"/> if another monitor lies
    /// beside it.
    /// </param>
    /// <param name="allowRight">
    /// Whether the right edge of the area is an edge of the screen.
    /// </param>
    /// <returns>
    /// Where the pet hangs and which way it faces, or <see langword="null"/> if it is not close to the left or right
    /// edge. The vertical position stays where it was, within the area; if the area is lower than the window the top
    /// wins.
    /// </returns>
    public static EdgeSnapResult? Find(
        Point2 position,
        Point2 size,
        MovementBounds area,
        double snapDistance,
        bool allowLeft = true,
        bool allowRight = true)
    {
        var top = Math.Max(area.MinY, Math.Min(position.Y, area.MaxY));
        if (allowLeft && position.X - area.MinX <= snapDistance)
        {
            return new EdgeSnapResult(new Point2(area.MinX, top), 1);
        }

        if (allowRight && area.MaxX - position.X <= snapDistance)
        {
            return new EdgeSnapResult(new Point2(area.MaxX, top), -1);
        }

        return null;
    }
}
