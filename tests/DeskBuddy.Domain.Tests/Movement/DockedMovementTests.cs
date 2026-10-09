using AwesomeAssertions;
using DeskBuddy.Domain.Movement;

namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="DockedMovement"/>.
/// </summary>
public class DockedMovementTests
{
    /// <summary>
    /// The area used by the tests: x from 0 to 1000, with the taskbar line at y 800.
    /// </summary>
    private static readonly MovementBounds Bounds = new(0, 1000, 0, 800);

    /// <summary>
    /// The strategy under test.
    /// </summary>
    private readonly DockedMovement _movement = new();

    /// <summary>
    /// Creates a context at a position, walking at 100 units per second.
    /// </summary>
    /// <param name="x">
    /// Horizontal position.
    /// </param>
    /// <param name="y">
    /// Vertical position.
    /// </param>
    /// <returns>
    /// The context.
    /// </returns>
    private static MovementContext At(double x, double y = 800)
    {
        return new MovementContext(new Point2(x, y), new Point2(128, 128), Bounds, Cursor: null, Speed: 100);
    }

    /// <summary>
    /// A walk starts facing the direction the state machine picked.
    /// </summary>
    [Theory]
    [InlineData(-1, -1)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    public void BeginWalk_PreferredDirection_IsReturnedAsFacing(int preferred, int expected)
    {
        // Act
        var facing = _movement.BeginWalk(At(500), preferred);

        // Assert
        facing.Should().Be(expected);
    }

    /// <summary>
    /// Walking moves horizontally at the given speed and stays on the taskbar line.
    /// </summary>
    [Fact]
    public void Step_Walking_MovesAlongTheLine()
    {
        // Arrange
        _movement.BeginWalk(At(500), 1);

        // Act
        var step = _movement.Step(At(500), TimeSpan.FromSeconds(0.5));

        // Assert
        step.Position.Should().Be(new Point2(550, 800));
        step.Facing.Should().Be(1);
        step.Arrived.Should().BeFalse();
    }

    /// <summary>
    /// A pet that was off the line (for instance just dropped higher up) is put back on it.
    /// </summary>
    [Fact]
    public void Step_AboveTheLine_ReturnsToTheLine()
    {
        // Arrange
        _movement.BeginWalk(At(500, 300), -1);

        // Act
        var step = _movement.Step(At(500, 300), TimeSpan.FromSeconds(0.1));

        // Assert
        step.Position.Y.Should().Be(800);
    }

    /// <summary>
    /// At the right end the pet turns around.
    /// </summary>
    [Fact]
    public void Step_ReachesRightEnd_TurnsAround()
    {
        // Arrange
        _movement.BeginWalk(At(990), 1);

        // Act
        var step = _movement.Step(At(990), TimeSpan.FromSeconds(1));

        // Assert
        step.Position.X.Should().Be(1000);
        step.Facing.Should().Be(-1);
    }

    /// <summary>
    /// Settling puts the pet on the taskbar line and inside the horizontal range.
    /// </summary>
    [Fact]
    public void Settle_OffTheLine_SnapsToLine()
    {
        // Act
        var rest = _movement.Settle(new Point2(1500, 120), Bounds);

        // Assert
        rest.Should().Be(new Point2(1000, 800));
    }
}
