namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Where and how a pet starts to crawl along the edges of the screen.
/// </summary>
/// <param name="Phase">
/// <see cref="CrawlPhase.Wall"/> to climb a side, <see cref="CrawlPhase.Ceiling"/> to crawl along the top edge.
/// </param>
/// <param name="OnRightEdge">
/// Whether the pet climbs the right side rather than the left one; only meant for a start on a side.
/// </param>
/// <param name="ClimbingUp">
/// Whether the pet starts upward rather than downward; only meant for a start on a side.
/// </param>
/// <param name="Direction">
/// The direction to go first along the top edge: <c>-1</c> for left, <c>1</c> for right; only meant for a start on the
/// top edge.
/// </param>
/// <param name="Y">
/// The vertical position of the window's top-left corner to start from.
/// </param>
public readonly record struct CrawlStart(CrawlPhase Phase, bool OnRightEdge, bool ClimbingUp, int Direction, double Y);

/// <summary>
/// Decides when a pet starts to crawl along the edges of the screen: by chance when it walks into the end of its line,
/// or at once when it is dropped at an edge.
/// </summary>
public static class CrawlStarter
{
    /// <summary>
    /// Decides whether a pet that has just taken a walking step starts to climb the side of the screen it walked into.
    /// </summary>
    /// <param name="position">
    /// Where the walking step put the pet.
    /// </param>
    /// <param name="bounds">
    /// The range the pet may take.
    /// </param>
    /// <param name="chance">
    /// The chance, from 0 to 1, that a pet at the end of its line starts to climb.
    /// </param>
    /// <param name="random">
    /// The source of chance.
    /// </param>
    /// <param name="edges">
    /// Tells which edges are real edges of the screen. Only asked once the chance has come out in favor of climbing.
    /// </param>
    /// <returns>
    /// How the pet starts to climb, or <see langword="null"/> if it does not.
    /// </returns>
    public static CrawlStart? AtWalkEnd(
        Point2 position,
        MovementBounds bounds,
        double chance,
        Random random,
        IScreenEdges edges)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(edges);

        if (bounds.MaxX <= bounds.MinX)
        {
            return null;
        }

        var atLeft = position.X <= bounds.MinX;
        var atRight = position.X >= bounds.MaxX;
        if ((!atLeft && !atRight) || random.NextDouble() >= chance || edges.HasMonitorBeside(atLeft))
        {
            return null;
        }

        return new CrawlStart(CrawlPhase.Wall, OnRightEdge: atRight, ClimbingUp: true, Direction: 0, position.Y);
    }

    /// <summary>
    /// Decides whether a pet that was dropped at the top or at a side of the screen crawls along that edge.
    /// </summary>
    /// <param name="position">
    /// The top-left corner of the pet window where it was dropped.
    /// </param>
    /// <param name="size">
    /// The size of the pet window.
    /// </param>
    /// <param name="bounds">
    /// The range the pet may take.
    /// </param>
    /// <param name="snapDistance">
    /// How close to an edge the pet must be dropped.
    /// </param>
    /// <param name="random">
    /// The source of chance.
    /// </param>
    /// <param name="edges">
    /// Tells which edges are real edges of the screen.
    /// </param>
    /// <returns>
    /// How the pet starts to crawl, or <see langword="null"/> if it is not close to an edge. Dropped on the ground a pet
    /// can only climb up; higher up it goes either way. On the top edge the first direction is chosen by chance.
    /// </returns>
    public static CrawlStart? OnDrop(
        Point2 position,
        Point2 size,
        MovementBounds bounds,
        double snapDistance,
        Random random,
        IScreenEdges edges)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(edges);

        // The top of a monitor with another one above it is no edge of the screen.
        if (position.Y - bounds.MinY <= snapDistance && !edges.HasMonitorAbove())
        {
            return new CrawlStart(
                CrawlPhase.Ceiling,
                OnRightEdge: false,
                ClimbingUp: false,
                Direction: random.Next(2) == 0 ? -1 : 1,
                bounds.MinY);
        }

        var snapped = EdgeSnap.Find(
            position,
            size,
            bounds,
            snapDistance,
            allowLeft: !edges.HasMonitorBeside(true),
            allowRight: !edges.HasMonitorBeside(false));
        if (snapped is not { } snap)
        {
            return null;
        }

        var onGround = bounds.MaxY - snap.Position.Y <= snapDistance;
        return new CrawlStart(
            CrawlPhase.Wall,
            OnRightEdge: snap.Facing < 0,
            ClimbingUp: onGround || random.Next(2) == 0,
            Direction: 0,
            snap.Position.Y);
    }
}
