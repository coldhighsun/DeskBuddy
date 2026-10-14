using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Keeps the pet on the taskbar line: it only ever walks horizontally and turns around at the ends.
/// </summary>
public sealed class DockedMovement
{
    /// <summary>
    /// The current walking direction: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    private int _direction = 1;

    /// <summary>
    /// Prepares for a new walk.
    /// </summary>
    /// <param name="context">
    /// Where the pet is and where it may go.
    /// </param>
    /// <param name="preferredDirection">
    /// The horizontal direction the behavior state machine picked for this walk: <c>-1</c> or <c>1</c>.
    /// </param>
    /// <returns>
    /// Which way the sprite faces at the start of the walk: <c>-1</c> for left, <c>1</c> for right.
    /// </returns>
    public int BeginWalk(MovementContext context, int preferredDirection)
    {
        _direction = preferredDirection < 0 ? -1 : 1;
        return _direction;
    }

    /// <summary>
    /// Moves the pet one step.
    /// </summary>
    /// <param name="context">
    /// Where the pet is and where it may go.
    /// </param>
    /// <param name="delta">
    /// Time elapsed since the previous step.
    /// </param>
    /// <returns>
    /// The new position and facing.
    /// </returns>
    public MovementStep Step(MovementContext context, TimeSpan delta)
    {
        var (x, direction) = WalkMovement.Step(
            context.Position.X,
            _direction,
            context.Speed,
            delta,
            context.Bounds.MinX,
            context.Bounds.MaxX);
        _direction = direction;

        return new MovementStep(new Point2(x, context.Bounds.MaxY), direction);
    }

    /// <summary>
    /// Finds where the pet rests after being dropped: on the taskbar line.
    /// </summary>
    /// <param name="position">
    /// Where the pet is now.
    /// </param>
    /// <param name="bounds">
    /// The range the pet may take.
    /// </param>
    /// <returns>
    /// The position to put the pet at.
    /// </returns>
    public Point2 Settle(Point2 position, MovementBounds bounds)
    {
        return new Point2(bounds.Clamp(position).X, bounds.MaxY);
    }
}
