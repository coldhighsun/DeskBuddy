using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Keeps the pet on the taskbar line: it only ever walks horizontally and turns around at the ends.
/// </summary>
public sealed class DockedMovement : IMovementStrategy
{
    /// <summary>
    /// The current walking direction: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    private int _direction = 1;

    /// <inheritdoc />
    public int BeginWalk(MovementContext context, int preferredDirection)
    {
        _direction = preferredDirection < 0 ? -1 : 1;
        return _direction;
    }

    /// <inheritdoc />
    public bool FallsWhenDropped => true;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Point2 Settle(Point2 position, MovementBounds bounds)
    {
        return new Point2(bounds.Clamp(position).X, bounds.MaxY);
    }
}
