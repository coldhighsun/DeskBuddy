using AwesomeAssertions;
using DeskBuddy.Domain.Movement;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="RoamMovement"/>.
/// </summary>
public class RoamMovementTests
{
    /// <summary>
    /// The area used by the tests.
    /// </summary>
    private static readonly MovementBounds Bounds = new(0, 1000, 0, 600);

    /// <summary>
    /// The pet's size in the tests.
    /// </summary>
    private static readonly Point2 Size = new(128, 128);

    /// <summary>
    /// The fake clock shared with the strategy.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The tuning; chasing is off unless a test turns it on.
    /// </summary>
    private readonly RoamOptions _options = new() { ChaseChance = 0 };

    /// <summary>
    /// The strategy under test.
    /// </summary>
    private readonly RoamMovement _movement;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoamMovementTests"/> class.
    /// </summary>
    public RoamMovementTests()
    {
        _movement = new RoamMovement(_options, _clock, new Random(7));
    }

    /// <summary>
    /// Creates a context at a position, walking at 100 units per second.
    /// </summary>
    /// <param name="x">
    /// Horizontal position.
    /// </param>
    /// <param name="y">
    /// Vertical position.
    /// </param>
    /// <param name="cursor">
    /// The pointer, if known.
    /// </param>
    /// <returns>
    /// The context.
    /// </returns>
    private static MovementContext At(double x, double y, Point2? cursor = null)
    {
        return new MovementContext(new Point2(x, y), Size, Bounds, cursor, Speed: 100);
    }

    /// <summary>
    /// A wandering walk starts facing the preferred direction.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void BeginWalk_NoChase_FacesPreferredDirection(int direction)
    {
        // Act
        var facing = _movement.BeginWalk(At(500, 300), direction);

        // Assert
        facing.Should().Be(direction);
    }

    /// <summary>
    /// Wandering keeps to the speed and stays within the heading spread, so the pet never walks straight up or down.
    /// </summary>
    [Fact]
    public void Step_Wandering_TravelsAtSpeedWithinSpread()
    {
        // Arrange
        var maxAngle = _options.HeadingSpreadDegrees * Math.PI / 180;

        for (var i = 0; i < 50; i++)
        {
            _movement.BeginWalk(At(500, 300), 1);

            // Act
            var step = _movement.Step(At(500, 300), TimeSpan.FromSeconds(1));

            // Assert
            var dx = step.Position.X - 500;
            var dy = step.Position.Y - 300;
            Math.Sqrt((dx * dx) + (dy * dy)).Should().BeApproximately(100, 0.001);
            dx.Should().BeGreaterThan(0);
            Math.Abs(Math.Atan2(dy, dx)).Should().BeLessThanOrEqualTo(maxAngle + 0.0001);
        }
    }

    /// <summary>
    /// Walking for a long time never leaves the area, whichever way it starts.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Step_WanderingForLong_StaysInsideBounds(int direction)
    {
        // Arrange
        var position = new Point2(5, 590);
        _movement.BeginWalk(At(position.X, position.Y), direction);

        // Act
        for (var i = 0; i < 5000; i++)
        {
            var step = _movement.Step(At(position.X, position.Y), TimeSpan.FromMilliseconds(33));
            position = step.Position;

            // Assert
            position.X.Should().BeInRange(Bounds.MinX, Bounds.MaxX);
            position.Y.Should().BeInRange(Bounds.MinY, Bounds.MaxY);
        }
    }

    /// <summary>
    /// Hitting the right edge turns the sprite around.
    /// </summary>
    [Fact]
    public void Step_ReachesRightEdge_FacesLeft()
    {
        // Arrange
        _movement.BeginWalk(At(995, 300), 1);

        // Act
        var step = _movement.Step(At(995, 300), TimeSpan.FromSeconds(1));

        // Assert
        step.Position.X.Should().Be(1000);
        step.Facing.Should().Be(-1);
    }

    /// <summary>
    /// Hitting the bottom edge sends the pet back up.
    /// </summary>
    [Fact]
    public void Step_ReachesBottomEdge_HeadsUpAfterwards()
    {
        // Arrange: start at the bottom edge so any downward heading bounces.
        var position = new Point2(500, 600);
        _movement.BeginWalk(At(position.X, position.Y), 1);
        var first = _movement.Step(At(position.X, position.Y), TimeSpan.FromSeconds(1));

        // Act
        var second = _movement.Step(At(first.Position.X, first.Position.Y), TimeSpan.FromSeconds(1));

        // Assert
        first.Position.Y.Should().BeLessThanOrEqualTo(600);
        second.Position.Y.Should().BeLessThanOrEqualTo(600);
    }

    /// <summary>
    /// With a pointer far away and chasing certain, the walk heads for the pointer, facing it.
    /// </summary>
    [Fact]
    public void BeginWalk_ChaseCertainAndPointerFar_StartsChasing()
    {
        // Arrange
        _options.ChaseChance = 1;
        var context = At(100, 300, cursor: new Point2(900, 364));

        // Act
        var facing = _movement.BeginWalk(context, -1);
        var step = _movement.Step(context, TimeSpan.FromSeconds(1));

        // Assert
        facing.Should().Be(1);
        step.Position.X.Should().BeApproximately(100 + (100 * _options.ChaseSpeedFactor), 0.001);
        step.Position.Y.Should().BeApproximately(300, 0.001);
    }

    /// <summary>
    /// The chase ends with an arrival once the pet's center is within the arrival distance.
    /// </summary>
    [Fact]
    public void Step_ChaseCloseEnough_ArrivesAndStops()
    {
        // Arrange
        _options.ChaseChance = 1;
        var start = At(100, 300, cursor: new Point2(900, 364));
        _movement.BeginWalk(start, 1);

        // Act: the pet's center (164, 364) is far from the pointer at x 900; jump close to it.
        var near = At(900 - _options.ArriveDistance - (Size.X / 2) + 10, 300, cursor: new Point2(900, 364));
        var step = _movement.Step(near, TimeSpan.FromMilliseconds(33));

        // Assert
        step.Arrived.Should().BeTrue();
        step.Position.Should().Be(near.Position);
    }

    /// <summary>
    /// The pet never overshoots the arrival distance in one step.
    /// </summary>
    [Fact]
    public void Step_ChaseLongStep_DoesNotOvershoot()
    {
        // Arrange
        _options.ChaseChance = 1;
        var context = At(100, 300, cursor: new Point2(500, 364));
        _movement.BeginWalk(context, 1);

        // Act
        var step = _movement.Step(context, TimeSpan.FromSeconds(60));

        // Assert
        var centerX = step.Position.X + (Size.X / 2);
        var centerY = step.Position.Y + (Size.Y / 2);
        var distance = Math.Sqrt(Math.Pow(500 - centerX, 2) + Math.Pow(364 - centerY, 2));
        distance.Should().BeApproximately(_options.ArriveDistance, 0.001);
    }

    /// <summary>
    /// After a chase the cooldown prevents another one until it has passed.
    /// </summary>
    [Fact]
    public void BeginWalk_WithinCooldownOfLastChase_DoesNotChase()
    {
        // Arrange
        _options.ChaseChance = 1;
        var context = At(100, 300, cursor: new Point2(900, 364));
        _movement.BeginWalk(context, 1);
        _clock.Advance(_options.ChaseCooldown - TimeSpan.FromSeconds(1));

        // Act
        _movement.BeginWalk(context, 1);
        var step = _movement.Step(context, TimeSpan.FromSeconds(1));

        // Assert: a wander moves exactly at walking speed, a chase would be faster.
        var dx = step.Position.X - 100;
        var dy = step.Position.Y - 300;
        Math.Sqrt((dx * dx) + (dy * dy)).Should().BeApproximately(100, 0.001);
    }

    /// <summary>
    /// Once the cooldown has passed the pet may chase again.
    /// </summary>
    [Fact]
    public void BeginWalk_AfterCooldown_ChasesAgain()
    {
        // Arrange
        _options.ChaseChance = 1;
        var context = At(100, 300, cursor: new Point2(900, 364));
        _movement.BeginWalk(context, 1);
        _clock.Advance(_options.ChaseCooldown);

        // Act
        _movement.BeginWalk(context, -1);
        var step = _movement.Step(context, TimeSpan.FromSeconds(1));

        // Assert
        step.Position.X.Should().BeApproximately(100 + (100 * _options.ChaseSpeedFactor), 0.001);
    }

    /// <summary>
    /// A pointer next to the pet is not chased.
    /// </summary>
    [Fact]
    public void BeginWalk_PointerAlreadyClose_DoesNotChase()
    {
        // Arrange
        _options.ChaseChance = 1;
        var context = At(100, 300, cursor: new Point2(100 + 64 + 50, 364));

        // Act
        _movement.BeginWalk(context, -1);
        var step = _movement.Step(context, TimeSpan.FromSeconds(1));

        // Assert: wandering leftwards at plain speed.
        step.Position.X.Should().BeLessThan(100);
    }

    /// <summary>
    /// A pointer on another monitor is not chased.
    /// </summary>
    [Fact]
    public void BeginWalk_PointerOffThisMonitor_DoesNotChase()
    {
        // Arrange
        _options.ChaseChance = 1;
        var context = At(500, 300, cursor: new Point2(-800, 300));

        // Act
        _movement.BeginWalk(context, 1);
        var step = _movement.Step(context, TimeSpan.FromSeconds(1));

        // Assert: it wanders to the right as asked rather than running left.
        step.Position.X.Should().BeGreaterThan(500);
    }

    /// <summary>
    /// Without a known pointer there is no chase.
    /// </summary>
    [Fact]
    public void BeginWalk_NoPointer_DoesNotChase()
    {
        // Arrange
        _options.ChaseChance = 1;
        var context = At(500, 300);

        // Act
        _movement.BeginWalk(context, 1);
        var step = _movement.Step(context, TimeSpan.FromSeconds(1));

        // Assert
        step.Arrived.Should().BeFalse();
        step.Position.X.Should().BeGreaterThan(500);
    }

    /// <summary>
    /// Settling keeps the pet where it was dropped, inside the area.
    /// </summary>
    [Fact]
    public void Settle_InsideBounds_KeepsPosition()
    {
        // Act
        var rest = _movement.Settle(new Point2(400, 250), Bounds);

        // Assert
        rest.Should().Be(new Point2(400, 250));
    }

    /// <summary>
    /// Settling pulls a pet dropped outside the area back in.
    /// </summary>
    [Fact]
    public void Settle_OutsideBounds_ClampsIntoArea()
    {
        // Act
        var rest = _movement.Settle(new Point2(-50, 900), Bounds);

        // Assert
        rest.Should().Be(new Point2(0, 600));
    }
}
