namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Tells whether a walking pet has room to hop, so that a hop does not end against the edge of the screen.
/// </summary>
public static class HopRoom
{
    /// <summary>
    /// Works out how much room a hop needs in the direction the pet walks: what it covers during the hop, which it does
    /// faster than it walks.
    /// </summary>
    /// <param name="walkSpeed">
    /// The walking speed, in device-independent pixels per second.
    /// </param>
    /// <param name="hopSpeedFactor">
    /// How much faster than it walks the pet moves forward during a hop.
    /// </param>
    /// <param name="hopDuration">
    /// How long the hop lasts.
    /// </param>
    /// <returns>
    /// The room, in device-independent pixels.
    /// </returns>
    public static double Required(double walkSpeed, double hopSpeedFactor, TimeSpan hopDuration)
    {
        return walkSpeed * hopSpeedFactor * hopDuration.TotalSeconds;
    }

    /// <summary>
    /// Tells whether there is enough room between the pet and the edge it walks towards.
    /// </summary>
    /// <param name="position">
    /// The position of the pet's top-left corner.
    /// </param>
    /// <param name="facing">
    /// The direction the pet walks: <c>-1</c> for left, <c>1</c> for right.
    /// </param>
    /// <param name="bounds">
    /// The range the pet may take.
    /// </param>
    /// <param name="required">
    /// The room needed, from <see cref="Required"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the pet can cover <paramref name="required"/> before it reaches the edge.
    /// </returns>
    public static bool IsClear(Point2 position, int facing, MovementBounds bounds, double required)
    {
        var room = facing < 0 ? position.X - bounds.MinX : bounds.MaxX - position.X;
        return room >= required;
    }
}
