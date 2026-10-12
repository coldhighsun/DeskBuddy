namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Tells which edges of the area the pet moves in are real edges of the screen, as opposed to a seam to another
/// monitor.
/// </summary>
public interface IScreenEdges
{
    /// <summary>
    /// Tells whether another monitor lies right beside the left or right edge.
    /// </summary>
    /// <param name="left">
    /// <see langword="true"/> to look beside the left edge, <see langword="false"/> for the right edge.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if there is a monitor there, so that the edge is only a seam between two monitors.
    /// </returns>
    bool HasMonitorBeside(bool left);

    /// <summary>
    /// Tells whether another monitor lies right above the top edge.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if there is a monitor there, so that the top edge is only a seam between two monitors.
    /// </returns>
    bool HasMonitorAbove();
}
