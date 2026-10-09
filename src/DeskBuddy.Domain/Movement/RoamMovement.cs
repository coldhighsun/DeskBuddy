namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Lets the pet wander over the whole work area. Each walk heads in a random direction (within a spread around the
/// horizontal) and bounces off the edges; now and then, subject to a cooldown, a walk heads for the mouse pointer
/// instead and ends when the pet gets close to it.
/// </summary>
/// <param name="options">
/// The roaming tuning.
/// </param>
/// <param name="timeProvider">
/// The time source for the chase cooldown.
/// </param>
/// <param name="random">
/// The random source for headings and for deciding to chase.
/// </param>
public sealed class RoamMovement(RoamOptions options, TimeProvider timeProvider, Random random) : IMovementStrategy
{
    /// <summary>
    /// Horizontal component of the wandering heading below which the facing is left unchanged.
    /// </summary>
    private const double FacingDeadZone = 0.05;

    /// <summary>
    /// The unit vector the pet is wandering along.
    /// </summary>
    private Point2 _heading = new(1, 0);

    /// <summary>
    /// Which way the sprite faces: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    private int _facing = 1;

    /// <summary>
    /// Whether the current walk is a chase after the mouse pointer.
    /// </summary>
    private bool _chasing;

    /// <summary>
    /// When the last chase started, or <see langword="null"/> if there has been none.
    /// </summary>
    private DateTimeOffset? _lastChaseAt;

    /// <inheritdoc />
    public int BeginWalk(MovementContext context, int preferredDirection)
    {
        var direction = preferredDirection < 0 ? -1 : 1;
        _chasing = false;

        if (CanChase(context) && random.NextDouble() < options.ChaseChance)
        {
            _chasing = true;
            _lastChaseAt = timeProvider.GetUtcNow();
            _facing = FacingToward(context.Cursor!.Value, context, direction);
            return _facing;
        }

        var spread = options.HeadingSpreadDegrees * Math.PI / 180;
        var angle = (random.NextDouble() * 2 - 1) * spread;
        _heading = new Point2(direction * Math.Cos(angle), Math.Sin(angle));
        _facing = direction;
        return _facing;
    }

    /// <inheritdoc />
    public MovementStep Step(MovementContext context, TimeSpan delta)
    {
        return _chasing && context.Cursor is { } cursor
            ? StepTowardCursor(context, cursor, delta)
            : Wander(context, delta);
    }

    /// <inheritdoc />
    public Point2 Settle(Point2 position, MovementBounds bounds)
    {
        return bounds.Clamp(position);
    }

    /// <summary>
    /// Determines whether a chase may start now: the cooldown has passed, the pointer is on this monitor, and the
    /// pet is not already next to it.
    /// </summary>
    /// <param name="context">
    /// Where the pet is and where the pointer is.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a chase is allowed.
    /// </returns>
    private bool CanChase(MovementContext context)
    {
        if (context.Cursor is not { } cursor || options.ChaseChance <= 0)
        {
            return false;
        }

        if (_lastChaseAt is { } last && timeProvider.GetUtcNow() - last < options.ChaseCooldown)
        {
            return false;
        }

        var bounds = context.Bounds;
        var onThisMonitor = cursor.X >= bounds.MinX
            && cursor.X <= bounds.MaxX + context.Size.X
            && cursor.Y >= bounds.MinY
            && cursor.Y <= bounds.MaxY + context.Size.Y;

        return onThisMonitor && DistanceToCenter(context, cursor) > options.ArriveDistance * 1.5;
    }

    /// <summary>
    /// Moves along the wandering heading, bouncing off the edges of the area.
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
    private MovementStep Wander(MovementContext context, TimeSpan delta)
    {
        var bounds = context.Bounds;
        var distance = context.Speed * delta.TotalSeconds;
        var x = context.Position.X + (_heading.X * distance);
        var y = context.Position.Y + (_heading.Y * distance);
        var (hx, hy) = (_heading.X, _heading.Y);

        var maxX = Math.Max(bounds.MinX, bounds.MaxX);
        var maxY = Math.Max(bounds.MinY, bounds.MaxY);
        if (x <= bounds.MinX)
        {
            (x, hx) = (bounds.MinX, Math.Abs(hx));
        }
        else if (x >= maxX)
        {
            (x, hx) = (maxX, -Math.Abs(hx));
        }

        if (y <= bounds.MinY)
        {
            (y, hy) = (bounds.MinY, Math.Abs(hy));
        }
        else if (y >= maxY)
        {
            (y, hy) = (maxY, -Math.Abs(hy));
        }

        _heading = new Point2(hx, hy);
        if (Math.Abs(hx) > FacingDeadZone)
        {
            _facing = hx < 0 ? -1 : 1;
        }

        return new MovementStep(new Point2(x, y), _facing);
    }

    /// <summary>
    /// Runs toward the mouse pointer and stops once close enough.
    /// </summary>
    /// <param name="context">
    /// Where the pet is and where it may go.
    /// </param>
    /// <param name="cursor">
    /// The mouse pointer.
    /// </param>
    /// <param name="delta">
    /// Time elapsed since the previous step.
    /// </param>
    /// <returns>
    /// The new position and facing; <see cref="MovementStep.Arrived"/> is set on the step that gets close enough.
    /// </returns>
    private MovementStep StepTowardCursor(MovementContext context, Point2 cursor, TimeSpan delta)
    {
        var dx = cursor.X - (context.Position.X + (context.Size.X / 2));
        var dy = cursor.Y - (context.Position.Y + (context.Size.Y / 2));
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance <= options.ArriveDistance)
        {
            _chasing = false;
            return new MovementStep(context.Position, _facing, Arrived: true);
        }

        if (Math.Abs(dx) > 1)
        {
            _facing = dx < 0 ? -1 : 1;
        }

        // Do not run past the arrival distance in a single step.
        var travel = Math.Min(context.Speed * options.ChaseSpeedFactor * delta.TotalSeconds, distance - options.ArriveDistance);
        var next = context.Bounds.Clamp(new Point2(
            context.Position.X + (dx / distance * travel),
            context.Position.Y + (dy / distance * travel)));

        return new MovementStep(next, _facing, Arrived: false);
    }

    /// <summary>
    /// Gets the distance from the center of the pet to a point.
    /// </summary>
    /// <param name="context">
    /// Where the pet is.
    /// </param>
    /// <param name="point">
    /// The point.
    /// </param>
    /// <returns>
    /// The distance.
    /// </returns>
    private static double DistanceToCenter(MovementContext context, Point2 point)
    {
        var dx = point.X - (context.Position.X + (context.Size.X / 2));
        var dy = point.Y - (context.Position.Y + (context.Size.Y / 2));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Gets which way the sprite should face to look at a point.
    /// </summary>
    /// <param name="point">
    /// The point to look at.
    /// </param>
    /// <param name="context">
    /// Where the pet is.
    /// </param>
    /// <param name="fallback">
    /// The facing to use when the point is straight above or below the pet.
    /// </param>
    /// <returns>
    /// <c>-1</c> for left, <c>1</c> for right.
    /// </returns>
    private static int FacingToward(Point2 point, MovementContext context, int fallback)
    {
        var dx = point.X - (context.Position.X + (context.Size.X / 2));
        return Math.Abs(dx) <= 1 ? fallback : dx < 0 ? -1 : 1;
    }
}
