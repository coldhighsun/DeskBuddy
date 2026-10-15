namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="HopLift"/>, including the loop the pet window runs for every walk step: ask the strategy for a
/// step from the walking line, raise it by the hop, move the window there, and read the walking line back from where the
/// window is.
/// </summary>
public class HopLiftTests
{
    /// <summary>
    /// How long the hops in the tests last.
    /// </summary>
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// The step used by the walk loop tests.
    /// </summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(33);

    /// <summary>
    /// The area used by the tests: the taskbar line is at y 800 and the top of the screen at 0.
    /// </summary>
    private static readonly MovementBounds Bounds = new(0, 1000, 0, 800);

    /// <summary>
    /// The lift under test.
    /// </summary>
    private readonly HopLift _lift = new();

    /// <summary>
    /// A new lift has not raised anything.
    /// </summary>
    [Fact]
    public void Constructor_Default_IsNotLifted()
    {
        // Assert
        _lift.Offset.Should().Be(0);
        _lift.IsLifted.Should().BeFalse();
        _lift.ToWalkingLine(new Point2(10, 700)).Should().Be(new Point2(10, 700));
    }

    /// <summary>
    /// At the top of the hop the window is raised by the height of the hop and the offset says by how much.
    /// </summary>
    [Fact]
    public void Lift_AtTopOfTheHop_RaisesByTheHeightOfTheHop()
    {
        // Act
        var raised = _lift.Lift(new Point2(100, 800), Duration / 2, Duration, petHeight: 100, heightRatio: 0.3, minY: 0);

        // Assert
        raised.Should().Be(new Point2(100, 770));
        _lift.Offset.Should().BeApproximately(30, 1e-9);
        _lift.IsLifted.Should().BeTrue();
    }

    /// <summary>
    /// Asking for a lift while the pet is not hopping puts the window back on the line.
    /// </summary>
    [Fact]
    public void Lift_NotHopping_PutsTheWindowOnTheLine()
    {
        // Arrange
        _lift.Lift(new Point2(100, 800), Duration / 2, Duration, 100, 0.3, 0);

        // Act
        var position = _lift.Lift(new Point2(100, 800), hopElapsed: null, Duration, 100, 0.3, 0);

        // Assert
        position.Should().Be(new Point2(100, 800));
        _lift.IsLifted.Should().BeFalse();
    }

    /// <summary>
    /// A hop never lifts the window above the top of the screen, and the offset only counts what was really lifted.
    /// </summary>
    [Fact]
    public void Lift_NearTheTopOfTheScreen_StopsAtTheTop()
    {
        // Act
        var raised = _lift.Lift(new Point2(100, 10), Duration / 2, Duration, petHeight: 100, heightRatio: 0.3, minY: 0);

        // Assert
        raised.Y.Should().Be(0);
        _lift.Offset.Should().Be(10);
    }

    /// <summary>
    /// A pet that already is above the highest place the hop may reach is not pulled down by the hop.
    /// </summary>
    [Fact]
    public void Lift_AlreadyAboveTheTopLimit_IsNotMoved()
    {
        // Act
        var raised = _lift.Lift(new Point2(100, -5), Duration / 2, Duration, 100, 0.3, minY: 0);

        // Assert
        raised.Y.Should().Be(-5);
        _lift.Offset.Should().Be(0);
    }

    /// <summary>
    /// The walking line is read back from the raised window by adding the offset.
    /// </summary>
    [Fact]
    public void ToWalkingLine_WhileLifted_AddsTheOffset()
    {
        // Arrange
        var raised = _lift.Lift(new Point2(100, 800), Duration / 2, Duration, 100, 0.3, 0);

        // Act
        var line = _lift.ToWalkingLine(raised);

        // Assert
        line.Should().Be(new Point2(100, 800));
    }

    /// <summary>
    /// After a reset the window counts as being on the line.
    /// </summary>
    [Fact]
    public void Reset_WhileLifted_ForgetsTheOffset()
    {
        // Arrange
        _lift.Lift(new Point2(100, 800), Duration / 2, Duration, 100, 0.3, 0);

        // Act
        _lift.Reset();

        // Assert
        _lift.IsLifted.Should().BeFalse();
        _lift.ToWalkingLine(new Point2(100, 770)).Should().Be(new Point2(100, 770));
    }

    /// <summary>
    /// Walking docked through a whole hop, the walk stays on the taskbar line and the pet is back on it when the hop is
    /// over: the lift does not pile up in the walk.
    /// </summary>
    [Fact]
    public void WalkLoop_DockedThroughAHop_StaysOnTheLineAndLandsOnIt()
    {
        // Arrange
        var movement = new DockedMovement();
        var window = new Point2(100, Bounds.MaxY);
        var highest = Bounds.MaxY;

        // Act
        for (var elapsed = TimeSpan.Zero; elapsed <= Duration + Tick + Tick; elapsed += Tick)
        {
            var context = new MovementContext(_lift.ToWalkingLine(window), new Point2(64, 64), Bounds, Speed: 100);
            var step = movement.Step(context, Tick);
            window = _lift.Lift(step.Position, elapsed < Duration ? elapsed : null, Duration, 64, 0.3, Bounds.MinY);
            highest = Math.Min(highest, window.Y);
        }

        // Assert
        highest.Should().BeLessThan(Bounds.MaxY - 15);
        window.Y.Should().Be(Bounds.MaxY);
        _lift.IsLifted.Should().BeFalse();
    }
}
