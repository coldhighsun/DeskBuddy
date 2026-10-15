namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="EdgeCrawl"/>.
/// </summary>
public class EdgeCrawlTests
{
    /// <summary>
    /// A step of one second, which also is the time between two moments at which the pet may turn.
    /// </summary>
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The range the pet may take: 1000 wide, 600 high.
    /// </summary>
    private static readonly MovementBounds Bounds = new(0, 1000, 0, 600);

    /// <summary>
    /// The tuning used by the tests; the time between turns and on the top edge is fixed.
    /// </summary>
    private readonly PetBehaviorOptions _options = new()
    {
        ClimbSpeed = 30,
        CeilingTurnIntervalMin = OneSecond,
        CeilingTurnIntervalMax = OneSecond,
        CeilingDurationMin = TimeSpan.FromSeconds(5),
        CeilingDurationMax = TimeSpan.FromSeconds(5),
    };

    /// <summary>
    /// Creates a crawl whose chances come out as the given values, one after the other; the last value repeats.
    /// </summary>
    /// <param name="chances">
    /// The values, each from 0 to 1.
    /// </param>
    /// <returns>
    /// The crawl.
    /// </returns>
    private EdgeCrawl CreateCrawl(params double[] chances)
    {
        return new EdgeCrawl(_options, new ScriptedRandom(chances));
    }

    /// <summary>
    /// The sprite faces the picture as drawn at the left edge and mirrored at the right edge.
    /// </summary>
    /// <param name="onRightEdge">
    /// Whether the pet is on the right edge.
    /// </param>
    /// <param name="expected">
    /// The expected facing.
    /// </param>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, -1)]
    public void StartOnWall_Edge_ReturnsFacing(bool onRightEdge, int expected)
    {
        // Arrange
        var crawl = CreateCrawl(0.99);

        // Act
        var facing = crawl.StartOnWall(onRightEdge, climbingUp: true);

        // Assert
        facing.Should().Be(expected);
        crawl.Phase.Should().Be(CrawlPhase.Wall);
    }

    /// <summary>
    /// The crawl says which state the pet is in: climbing up or down the side it started on.
    /// </summary>
    /// <param name="climbingUp">
    /// Whether the pet starts by climbing up.
    /// </param>
    /// <param name="expected">
    /// The state that goes with it.
    /// </param>
    [Theory]
    [InlineData(true, PetState.Climb)]
    [InlineData(false, PetState.ClimbDown)]
    public void CrawlState_AfterStartOnWall_MatchesDirection(bool climbingUp, PetState expected)
    {
        // Arrange
        var crawl = CreateCrawl(0.99);

        // Act
        crawl.StartOnWall(onRightEdge: false, climbingUp: climbingUp);

        // Assert
        crawl.CrawlState.Should().Be(expected);
    }

    /// <summary>
    /// On the top edge the pet is in the ceiling state.
    /// </summary>
    [Fact]
    public void CrawlState_AfterStartOnCeiling_IsCeiling()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);

        // Act
        crawl.StartOnCeiling(1);

        // Assert
        crawl.CrawlState.Should().Be(PetState.Ceiling);
    }

    /// <summary>
    /// A pet that climbs up over the top and along the ceiling to a corner comes down head first: the state follows
    /// from climbing to hanging to climbing down, and the pet really goes down the side.
    /// </summary>
    [Fact]
    public void CrawlState_UpTheSideAlongTheTopAndDown_ChangesWithTheRoute()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnWall(onRightEdge: false, climbingUp: true);
        var states = new List<PetState> { crawl.CrawlState };

        // Act: over the top, then out of time on the ceiling 5 px from the left corner, then one second down the side.
        var overTheTop = crawl.Step(new Point2(0, 10), Bounds, OneSecond);
        states.Add(crawl.CrawlState);
        var atCorner = crawl.Step(new Point2(5, 0), Bounds, TimeSpan.FromSeconds(5));
        states.Add(crawl.CrawlState);
        var downTheSide = crawl.Step(atCorner.Position, Bounds, OneSecond);
        states.Add(crawl.CrawlState);

        // Assert
        overTheTop.Phase.Should().Be(CrawlPhase.Ceiling);
        atCorner.Phase.Should().Be(CrawlPhase.Wall);
        downTheSide.Position.Should().Be(new Point2(0, 30));
        states.Should().Equal(PetState.Climb, PetState.Ceiling, PetState.ClimbDown, PetState.ClimbDown);
    }

    /// <summary>
    /// A pet climbing up moves up at the climbing speed, staying at the edge.
    /// </summary>
    [Fact]
    public void Step_ClimbingUp_MovesUpAtClimbSpeed()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnWall(onRightEdge: false, climbingUp: true);

        // Act
        var step = crawl.Step(new Point2(0, 300), Bounds, TimeSpan.FromMilliseconds(500));

        // Assert
        step.Position.Should().Be(new Point2(0, 285));
        step.Phase.Should().Be(CrawlPhase.Wall);
        step.Outcome.Should().Be(CrawlOutcome.Continue);
    }

    /// <summary>
    /// A pet on the right edge stays at the right end of the range.
    /// </summary>
    [Fact]
    public void Step_OnRightEdge_StaysAtRightEnd()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnWall(onRightEdge: true, climbingUp: true);

        // Act
        var step = crawl.Step(new Point2(1000, 300), Bounds, TimeSpan.FromMilliseconds(500));

        // Assert
        step.Position.X.Should().Be(1000);
        step.Facing.Should().Be(-1);
    }

    /// <summary>
    /// A pet that climbs up to the top goes on along the top edge, away from the side it came from.
    /// </summary>
    [Fact]
    public void Step_ClimbingUpReachesTop_SwitchesToCeiling()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnWall(onRightEdge: false, climbingUp: true);

        // Act
        var step = crawl.Step(new Point2(0, 10), Bounds, OneSecond);

        // Assert
        step.Phase.Should().Be(CrawlPhase.Ceiling);
        step.Position.Should().Be(new Point2(0, 0));
        step.Facing.Should().Be(1);
        step.Outcome.Should().Be(CrawlOutcome.Continue);
        crawl.Phase.Should().Be(CrawlPhase.Ceiling);
    }

    /// <summary>
    /// A pet that climbs down to the ground is done crawling, and stands on the ground.
    /// </summary>
    [Fact]
    public void Step_ClimbingDownReachesGround_Ends()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnWall(onRightEdge: false, climbingUp: false);

        // Act
        var step = crawl.Step(new Point2(0, 590), Bounds, OneSecond);

        // Assert
        step.Outcome.Should().Be(CrawlOutcome.ReachedBottom);
        step.Position.Should().Be(new Point2(0, 600));
    }

    /// <summary>
    /// Once it starts up or down a side, the pet goes all the way, whatever the chances are and however long it takes.
    /// </summary>
    [Fact]
    public void Step_OnWall_NeverTurnsOrLetsGo()
    {
        // Arrange: every chance would turn the pet around or make it let go.
        var crawl = CreateCrawl(0.0);
        crawl.StartOnWall(onRightEdge: false, climbingUp: true);
        var position = new Point2(0, 300);

        // Act
        var step = crawl.Step(position, Bounds, TimeSpan.FromSeconds(5));

        // Assert
        step.Outcome.Should().Be(CrawlOutcome.Continue);
        step.Phase.Should().Be(CrawlPhase.Wall);
        step.Position.Y.Should().Be(150);
    }

    /// <summary>
    /// A pet going down a side carries on down to the ground, whatever the chances are.
    /// </summary>
    [Fact]
    public void Step_ClimbingDown_KeepsGoingDown()
    {
        // Arrange
        var crawl = CreateCrawl(0.0);
        crawl.StartOnWall(onRightEdge: true, climbingUp: false);

        // Act
        var step = crawl.Step(new Point2(1000, 100), Bounds, TimeSpan.FromSeconds(3));

        // Assert
        step.Outcome.Should().Be(CrawlOutcome.Continue);
        step.Position.Y.Should().Be(190);
    }

    /// <summary>
    /// At a moment at which the pet may turn on the top edge, the chance can make it turn around.
    /// </summary>
    [Fact]
    public void Step_CeilingTurnMomentWithReverse_TurnsAround()
    {
        // Arrange
        var crawl = CreateCrawl(0.0);
        crawl.StartOnCeiling(1);

        // Act
        var step = crawl.Step(new Point2(500, 0), Bounds, OneSecond);

        // Assert
        step.Position.Should().Be(new Point2(470, 0));
        step.Facing.Should().Be(-1);
        step.Outcome.Should().Be(CrawlOutcome.Continue);
    }

    /// <summary>
    /// A pet told to hold its course does not turn around at a moment at which it otherwise would, and goes on the same
    /// way.
    /// </summary>
    [Fact]
    public void Step_CeilingTurnMomentWhileHoldingCourse_KeepsDirection()
    {
        // Arrange
        var crawl = CreateCrawl(0.0);
        crawl.StartOnCeiling(1);

        // Act
        var step = crawl.Step(new Point2(500, 0), Bounds, OneSecond, holdCourse: true);

        // Assert
        step.Position.Should().Be(new Point2(530, 0));
        step.Facing.Should().Be(1);
        step.Outcome.Should().Be(CrawlOutcome.Continue);
    }

    /// <summary>
    /// A pet told to hold its course does not let go when its time on the top edge would be up, and the time does not run
    /// down meanwhile; once it may decide again, it lets go when the time is up.
    /// </summary>
    [Fact]
    public void Step_CeilingTimeUpWhileHoldingCourse_KeepsCrawlingUntilReleased()
    {
        // Arrange
        var crawl = CreateCrawl(0.0);
        crawl.StartOnCeiling(1);

        // Act
        var held = crawl.Step(new Point2(500, 0), Bounds, TimeSpan.FromSeconds(5), holdCourse: true);
        var released = crawl.Step(held.Position, Bounds, TimeSpan.FromSeconds(5));

        // Assert
        held.Outcome.Should().Be(CrawlOutcome.Continue);
        held.Phase.Should().Be(CrawlPhase.Ceiling);
        held.Position.X.Should().Be(650);
        released.Outcome.Should().Be(CrawlOutcome.Dropped);
    }

    /// <summary>
    /// On the top edge the pet moves along it at the climbing speed, facing the way it goes.
    /// </summary>
    /// <param name="direction">
    /// The direction to go first.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Step_OnCeiling_MovesAlongTopEdge(int direction)
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnCeiling(direction);

        // Act
        var step = crawl.Step(new Point2(500, 0), Bounds, OneSecond);

        // Assert
        step.Position.Should().Be(new Point2(500 + (direction * 30), 0));
        step.Phase.Should().Be(CrawlPhase.Ceiling);
        step.Facing.Should().Be(direction);
    }

    /// <summary>
    /// A pet that reaches a corner while it still has time on the top edge turns around.
    /// </summary>
    [Fact]
    public void Step_CeilingCornerWithTimeLeft_TurnsAround()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnCeiling(1);

        // Act
        var step = crawl.Step(new Point2(995, 0), Bounds, OneSecond);

        // Assert
        step.Position.X.Should().Be(1000);
        step.Facing.Should().Be(-1);
        step.Phase.Should().Be(CrawlPhase.Ceiling);
    }

    /// <summary>
    /// When its time on the top edge is up, the chance can make the pet let go of it.
    /// </summary>
    [Fact]
    public void Step_CeilingTimeUpWithDrop_LetsGo()
    {
        // Arrange
        var crawl = CreateCrawl(0.0);
        crawl.StartOnCeiling(1);

        // Act
        var step = crawl.Step(new Point2(500, 0), Bounds, TimeSpan.FromSeconds(5));

        // Assert
        step.Outcome.Should().Be(CrawlOutcome.Dropped);
        step.Position.Should().Be(new Point2(500, 0));
    }

    /// <summary>
    /// When its time on the top edge is up and it does not let go, the pet goes to the nearest corner and climbs down the
    /// side there.
    /// </summary>
    [Fact]
    public void Step_CeilingTimeUpWithoutDrop_ClimbsDownAtNearestCorner()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnCeiling(1);

        // Act: out of time, 5 px from the left corner.
        var step = crawl.Step(new Point2(5, 0), Bounds, TimeSpan.FromSeconds(5));
        var next = crawl.Step(step.Position, Bounds, OneSecond);

        // Assert
        step.Phase.Should().Be(CrawlPhase.Wall);
        step.Position.Should().Be(new Point2(0, 0));
        step.Facing.Should().Be(1);
        next.Position.Should().Be(new Point2(0, 30));
        next.Phase.Should().Be(CrawlPhase.Wall);
    }

    /// <summary>
    /// A pet that leaves the top edge at the right corner climbs down the right side, facing the mirrored way.
    /// </summary>
    [Fact]
    public void Step_CeilingTimeUpAtRightCorner_ClimbsDownRightSide()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnCeiling(1);

        // Act: out of time, 5 px from the right corner.
        var step = crawl.Step(new Point2(995, 0), Bounds, TimeSpan.FromSeconds(5));
        var next = crawl.Step(step.Position, Bounds, OneSecond);

        // Assert
        step.Phase.Should().Be(CrawlPhase.Wall);
        step.Position.Should().Be(new Point2(1000, 0));
        step.Facing.Should().Be(-1);
        next.Position.Should().Be(new Point2(1000, 30));
        next.Outcome.Should().Be(CrawlOutcome.Continue);
    }

    /// <summary>
    /// A pet on its way to a corner that gets to an end which is only a seam to another monitor turns around instead of
    /// climbing down, and goes on to the other end.
    /// </summary>
    [Fact]
    public void Step_CornerEndIsSeam_TurnsAroundInsteadOfClimbingDown()
    {
        // Arrange: the right end is a seam, the left one an edge of the screen.
        var crawl = CreateCrawl(0.99);
        crawl.StartOnCeiling(1);
        var edges = new FakeEdges(left: false, right: true);

        // Act: out of time near the right end, so the pet heads for it; it gets there, then goes on to the left end.
        var heading = crawl.Step(new Point2(990, 0), Bounds, TimeSpan.FromSeconds(5), edges: edges);
        var turned = crawl.Step(heading.Position, Bounds, OneSecond, edges: edges);

        // Assert
        heading.Phase.Should().Be(CrawlPhase.Ceiling);
        heading.Position.X.Should().Be(1000);
        heading.Facing.Should().Be(-1);
        turned.Phase.Should().Be(CrawlPhase.Ceiling);
        turned.Position.X.Should().Be(970);
    }

    /// <summary>
    /// A pet on its way to a corner that finds a seam at both ends has no side to climb down, and lets go.
    /// </summary>
    [Fact]
    public void Step_BothEndsAreSeams_LetsGoAfterTurningOnce()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnCeiling(1);
        var edges = new FakeEdges(left: true, right: true);

        // Act: heads for the right end, turns at it, crosses to the left end.
        var atRight = crawl.Step(new Point2(990, 0), Bounds, TimeSpan.FromSeconds(5), edges: edges);
        var atLeft = crawl.Step(new Point2(5, 0), Bounds, OneSecond, edges: edges);

        // Assert
        atRight.Outcome.Should().Be(CrawlOutcome.Continue);
        atLeft.Outcome.Should().Be(CrawlOutcome.Dropped);
    }

    /// <summary>
    /// A pet that gets to a seam while it is not heading for a corner simply turns around, as at any end.
    /// </summary>
    [Fact]
    public void Step_EndIsSeamWhileNotHeadingForCorner_TurnsAround()
    {
        // Arrange
        var crawl = CreateCrawl(0.99);
        crawl.StartOnCeiling(1);

        // Act
        var step = crawl.Step(new Point2(995, 0), Bounds, OneSecond, edges: new FakeEdges(left: true, right: true));

        // Assert
        step.Phase.Should().Be(CrawlPhase.Ceiling);
        step.Facing.Should().Be(-1);
        step.Outcome.Should().Be(CrawlOutcome.Continue);
    }

    /// <summary>
    /// A fake that says whether the left and right ends are seams to other monitors.
    /// </summary>
    /// <param name="left">
    /// Whether another monitor lies beside the left end.
    /// </param>
    /// <param name="right">
    /// Whether another monitor lies beside the right end.
    /// </param>
    private sealed class FakeEdges(bool left, bool right) : IScreenEdges
    {
        /// <inheritdoc />
        public bool HasMonitorBeside(bool atLeft)
        {
            return atLeft ? left : right;
        }

        /// <inheritdoc />
        public bool HasMonitorAbove()
        {
            return false;
        }
    }

    /// <summary>
    /// A random source that gives the values it is told to, one after the other, and repeats the last.
    /// </summary>
    /// <param name="values">
    /// The values to give.
    /// </param>
    private sealed class ScriptedRandom(double[] values) : Random
    {
        /// <summary>
        /// The next value to give.
        /// </summary>
        private int _next;

        /// <inheritdoc />
        public override double NextDouble()
        {
            var value = values[Math.Min(_next, values.Length - 1)];
            _next++;
            return value;
        }
    }
}
