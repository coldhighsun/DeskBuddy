using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Where the crawling pet is: on the left or right side of the screen, or on its top edge.
/// </summary>
public enum CrawlPhase
{
    /// <summary>
    /// The pet climbs along the left or right edge of the screen.
    /// </summary>
    Wall,

    /// <summary>
    /// The pet hangs from the top edge of the screen.
    /// </summary>
    Ceiling,
}

/// <summary>
/// What a crawl step decided about the crawl as a whole.
/// </summary>
public enum CrawlOutcome
{
    /// <summary>
    /// The pet carries on crawling.
    /// </summary>
    Continue,

    /// <summary>
    /// The pet climbed down to the ground; the crawl is over.
    /// </summary>
    ReachedBottom,

    /// <summary>
    /// The pet let go of the edge; the crawl is over and the pet falls.
    /// </summary>
    Dropped,
}

/// <summary>
/// The result of one crawl step.
/// </summary>
/// <param name="Position">
/// The new position of the window's top-left corner.
/// </param>
/// <param name="Phase">
/// Whether the pet is now on a side of the screen or on its top edge.
/// </param>
/// <param name="Facing">
/// Which way the sprite faces: <c>1</c> for the picture as drawn, <c>-1</c> for the mirrored one. On a side this is
/// <c>1</c> at the left edge and <c>-1</c> at the right edge; on the top edge it is the direction of travel.
/// </param>
/// <param name="Outcome">
/// Whether the crawl goes on.
/// </param>
public readonly record struct CrawlStep(Point2 Position, CrawlPhase Phase, int Facing, CrawlOutcome Outcome);

/// <summary>
/// Moves a pet along the edges of the screen: up or down a side, and along the top. Once it starts up or down a side it
/// goes all the way, to the top or to the ground. On the top edge it now and then turns around, and eventually either
/// lets go or crawls to a corner and climbs down. All chances come from <see cref="PetBehaviorOptions"/>.
/// </summary>
/// <param name="options">
/// The tuning values.
/// </param>
/// <param name="random">
/// The source of chance.
/// </param>
public sealed class EdgeCrawl(PetBehaviorOptions options, Random random)
{
    /// <summary>
    /// The time left before the pet may turn around on the top edge again.
    /// </summary>
    private TimeSpan _untilTurn;

    /// <summary>
    /// The time left before the pet is done with the top edge.
    /// </summary>
    private TimeSpan _ceilingLeft;

    /// <summary>
    /// Whether the pet is on the right side of the screen rather than the left one.
    /// </summary>
    private bool _onRightEdge;

    /// <summary>
    /// Whether the pet climbs up rather than down a side.
    /// </summary>
    private bool _climbingUp;

    /// <summary>
    /// The direction of travel along the top edge: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    private int _direction = 1;

    /// <summary>
    /// Whether the pet is on its way to a corner to climb down.
    /// </summary>
    private bool _headingForCorner;

    /// <summary>
    /// Whether the pet, on its way to a corner, has already turned around at an end that was only a seam.
    /// </summary>
    private bool _turnedAtSeam;

    /// <summary>
    /// Gets where the pet is now.
    /// </summary>
    public CrawlPhase Phase { get; private set; }

    /// <summary>
    /// Gets the state the pet is in while it crawls like this: climbing up or down a side, or hanging from the top.
    /// </summary>
    public PetState CrawlState => Phase switch
    {
        CrawlPhase.Wall => _climbingUp ? PetState.Climb : PetState.ClimbDown,
        _ => PetState.Ceiling,
    };

    /// <summary>
    /// Starts a crawl on the left or right side of the screen.
    /// </summary>
    /// <param name="onRightEdge">
    /// Whether the pet clings to the right edge rather than the left one.
    /// </param>
    /// <param name="climbingUp">
    /// Whether the pet starts by climbing up rather than down.
    /// </param>
    /// <returns>
    /// Which way the sprite faces: <c>1</c> at the left edge, <c>-1</c> at the right edge.
    /// </returns>
    public int StartOnWall(bool onRightEdge, bool climbingUp)
    {
        Phase = CrawlPhase.Wall;
        _onRightEdge = onRightEdge;
        _climbingUp = climbingUp;
        _headingForCorner = false;
        return WallFacing;
    }

    /// <summary>
    /// Starts a crawl along the top edge.
    /// </summary>
    /// <param name="direction">
    /// The direction to go first: <c>-1</c> for left, <c>1</c> for right.
    /// </param>
    /// <returns>
    /// Which way the sprite faces.
    /// </returns>
    public int StartOnCeiling(int direction)
    {
        Phase = CrawlPhase.Ceiling;
        _direction = direction < 0 ? -1 : 1;
        _headingForCorner = false;
        _turnedAtSeam = false;
        _ceilingLeft = random.NextTimeSpan(options.CeilingDurationMin, options.CeilingDurationMax);
        _untilTurn = NextTurnInterval();
        return _direction;
    }

    /// <summary>
    /// Moves the pet one step.
    /// </summary>
    /// <param name="position">
    /// The window's top-left corner now.
    /// </param>
    /// <param name="bounds">
    /// The range the window's top-left corner may take; the top edge is <see cref="MovementBounds.MinY"/> and the
    /// ground is <see cref="MovementBounds.MaxY"/>.
    /// </param>
    /// <param name="delta">
    /// Time elapsed since the previous step.
    /// </param>
    /// <param name="holdCourse">
    /// Whether the pet has to keep going the way it goes, for instance because it lies across the seam between two
    /// monitors: on the top edge it then neither turns around, nor lets go, nor sets out for a corner, and the time for
    /// those does not run down.
    /// </param>
    /// <param name="edges">
    /// Tells whether the left or right end of the range is only a seam to another monitor rather than an edge of the
    /// screen; asked only when the pet gets to an end on the top edge. A pet heading for a corner turns around at a seam
    /// instead of climbing down there, and lets go if both ends are seams. <see langword="null"/> takes every end for an
    /// edge of the screen.
    /// </param>
    /// <returns>
    /// The new position, phase and facing, and whether the crawl goes on.
    /// </returns>
    public CrawlStep Step(
        Point2 position,
        MovementBounds bounds,
        TimeSpan delta,
        bool holdCourse = false,
        IScreenEdges? edges = null)
    {
        return Phase == CrawlPhase.Wall
            ? StepOnWall(position, bounds, delta)
            : StepOnCeiling(position, bounds, delta, holdCourse, edges);
    }

    /// <summary>
    /// Gets which way the sprite faces on a side of the screen.
    /// </summary>
    private int WallFacing => _onRightEdge ? -1 : 1;

    /// <summary>
    /// Moves the pet up or down a side of the screen, all the way to the top or to the ground.
    /// </summary>
    /// <param name="position">
    /// The window's top-left corner now.
    /// </param>
    /// <param name="bounds">
    /// The range the window's top-left corner may take.
    /// </param>
    /// <param name="delta">
    /// Time elapsed since the previous step.
    /// </param>
    /// <returns>
    /// The step.
    /// </returns>
    private CrawlStep StepOnWall(Point2 position, MovementBounds bounds, TimeSpan delta)
    {
        var x = _onRightEdge ? Math.Max(bounds.MinX, bounds.MaxX) : bounds.MinX;

        var y = position.Y + ((_climbingUp ? -1 : 1) * options.ClimbSpeed * delta.TotalSeconds);
        var floor = Math.Max(bounds.MinY, bounds.MaxY);

        if (y <= bounds.MinY)
        {
            // Over the top: on to the ceiling, heading away from the side it came from.
            var direction = _onRightEdge ? -1 : 1;
            var facing = StartOnCeiling(direction);
            return new CrawlStep(new Point2(x, bounds.MinY), CrawlPhase.Ceiling, facing, CrawlOutcome.Continue);
        }

        if (y >= floor)
        {
            return new CrawlStep(new Point2(x, floor), CrawlPhase.Wall, WallFacing, CrawlOutcome.ReachedBottom);
        }

        return new CrawlStep(new Point2(x, y), CrawlPhase.Wall, WallFacing, CrawlOutcome.Continue);
    }

    /// <summary>
    /// Moves the pet along the top edge of the screen.
    /// </summary>
    /// <param name="position">
    /// The window's top-left corner now.
    /// </param>
    /// <param name="bounds">
    /// The range the window's top-left corner may take.
    /// </param>
    /// <param name="delta">
    /// Time elapsed since the previous step.
    /// </param>
    /// <param name="holdCourse">
    /// Whether the pet keeps going the way it goes, without turning around, letting go or heading for a corner.
    /// </param>
    /// <param name="edges">
    /// Tells whether an end of the range is only a seam to another monitor, or <see langword="null"/> if all ends are
    /// corners of the screen.
    /// </param>
    /// <returns>
    /// The step.
    /// </returns>
    private CrawlStep StepOnCeiling(Point2 position, MovementBounds bounds, TimeSpan delta, bool holdCourse, IScreenEdges? edges)
    {
        var minX = bounds.MinX;
        var maxX = Math.Max(bounds.MinX, bounds.MaxX);

        if (!_headingForCorner && !holdCourse)
        {
            _ceilingLeft -= delta;
            if (_ceilingLeft <= TimeSpan.Zero)
            {
                if (random.NextDouble() < options.CeilingDropChance)
                {
                    return new CrawlStep(new Point2(position.X, bounds.MinY), CrawlPhase.Ceiling, _direction, CrawlOutcome.Dropped);
                }

                _headingForCorner = true;
                _direction = position.X - minX <= maxX - position.X ? -1 : 1;
            }
            else
            {
                _untilTurn -= delta;
                if (_untilTurn <= TimeSpan.Zero)
                {
                    _untilTurn = NextTurnInterval();
                    if (random.NextDouble() < options.CeilingReverseChance)
                    {
                        _direction = -_direction;
                    }
                }
            }
        }

        var x = position.X + (_direction * options.ClimbSpeed * delta.TotalSeconds);
        if (x <= minX || x >= maxX)
        {
            var atLeft = x <= minX;
            x = atLeft ? minX : maxX;

            // The end of the range is no corner of the screen when another monitor lies beside it (the monitor there has
            // no screen at this height, so the range stops short of it): there is no side to climb down.
            var atSeam = edges?.HasMonitorBeside(atLeft) == true;
            if (_headingForCorner && !atSeam)
            {
                // At the corner: down the side, all the way to the ground.
                var facing = StartOnWall(onRightEdge: !atLeft, climbingUp: false);
                return new CrawlStep(new Point2(x, bounds.MinY), CrawlPhase.Wall, facing, CrawlOutcome.Continue);
            }

            if (_headingForCorner && _turnedAtSeam)
            {
                // Both ends are seams: there is no corner to go to, so the pet lets go.
                return new CrawlStep(new Point2(x, bounds.MinY), CrawlPhase.Ceiling, _direction, CrawlOutcome.Dropped);
            }

            _turnedAtSeam |= _headingForCorner;
            _direction = atLeft ? 1 : -1;
        }

        return new CrawlStep(new Point2(x, bounds.MinY), CrawlPhase.Ceiling, _direction, CrawlOutcome.Continue);
    }

    /// <summary>
    /// Picks the time until the pet may next turn around on the top edge.
    /// </summary>
    /// <returns>
    /// A random time between the configured limits.
    /// </returns>
    private TimeSpan NextTurnInterval()
    {
        return random.NextTimeSpan(options.CeilingTurnIntervalMin, options.CeilingTurnIntervalMax);
    }
}
