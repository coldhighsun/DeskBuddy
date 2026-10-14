namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Keeps the lift of a hop apart from the walk. The window of a hopping pet is raised above the line it walks along, but
/// the movement strategy must go on working from the line itself, or the lift would pile up in the walk (the pet would
/// sink or drift step by step). This is the one place that knows the difference between the two positions.
/// </summary>
public sealed class HopLift
{
    /// <summary>
    /// Gets how far the window is raised above the line the pet walks along now, zero when it is not hopping.
    /// </summary>
    public double Offset { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the window is raised above the line the pet walks along.
    /// </summary>
    public bool IsLifted => Offset != 0;

    /// <summary>
    /// Works out where the pet walks, given where its window is.
    /// </summary>
    /// <param name="actual">
    /// The position of the window, raised by the hop if there is one.
    /// </param>
    /// <returns>
    /// The same position on the line the pet walks along.
    /// </returns>
    public Point2 ToWalkingLine(Point2 actual)
    {
        return actual with { Y = actual.Y + Offset };
    }

    /// <summary>
    /// Raises a walking position by the hop at the given moment, and remembers how far.
    /// </summary>
    /// <param name="walkingLine">
    /// The position the walk step worked out, on the line the pet walks along.
    /// </param>
    /// <param name="hopElapsed">
    /// The time since the hop began, or <see langword="null"/> if the pet is not hopping, which puts it back on the line.
    /// </param>
    /// <param name="duration">
    /// How long the hop lasts.
    /// </param>
    /// <param name="petHeight">
    /// The height of the pet window.
    /// </param>
    /// <param name="heightRatio">
    /// How high the top of the hop is, as a fraction of <paramref name="petHeight"/>.
    /// </param>
    /// <param name="minY">
    /// The highest the window may go, so that a hop never lifts the pet off the top of the screen.
    /// </param>
    /// <returns>
    /// The position the window is to be moved to.
    /// </returns>
    public Point2 Lift(Point2 walkingLine, TimeSpan? hopElapsed, TimeSpan duration, double petHeight, double heightRatio, double minY)
    {
        var lift = hopElapsed is { } elapsed ? HopArc.Offset(elapsed, duration, petHeight, heightRatio) : 0;
        var lifted = Math.Max(walkingLine.Y - lift, Math.Min(walkingLine.Y, minY));
        Offset = walkingLine.Y - lifted;
        return walkingLine with { Y = lifted };
    }

    /// <summary>
    /// Forgets the lift, as when the pet has been put back on the line or replaced by another.
    /// </summary>
    public void Reset()
    {
        Offset = 0;
    }
}
