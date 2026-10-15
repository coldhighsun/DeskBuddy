namespace DeskBuddy.Domain.Tests.Behavior;

/// <summary>
/// Tests for <see cref="WalkMovement"/>.
/// </summary>
public class WalkMovementTests
{
    /// <summary>
    /// Walking right advances by speed times elapsed time and keeps the direction.
    /// </summary>
    [Fact]
    public void Step_WalkingRight_AdvancesPosition()
    {
        // Act
        var (x, direction) = WalkMovement.Step(100, 1, 60, TimeSpan.FromSeconds(0.5), 0, 1000);

        // Assert
        x.Should().BeApproximately(130, 0.001);
        direction.Should().Be(1);
    }

    /// <summary>
    /// Walking left decreases the position and keeps the direction.
    /// </summary>
    [Fact]
    public void Step_WalkingLeft_DecreasesPosition()
    {
        // Act
        var (x, direction) = WalkMovement.Step(100, -1, 60, TimeSpan.FromSeconds(0.5), 0, 1000);

        // Assert
        x.Should().BeApproximately(70, 0.001);
        direction.Should().Be(-1);
    }

    /// <summary>
    /// Reaching the right end clamps the position and turns the pet around.
    /// </summary>
    [Fact]
    public void Step_ReachesRightEnd_ClampsAndTurnsLeft()
    {
        // Act
        var (x, direction) = WalkMovement.Step(990, 1, 60, TimeSpan.FromSeconds(1), 0, 1000);

        // Assert
        x.Should().Be(1000);
        direction.Should().Be(-1);
    }

    /// <summary>
    /// Reaching the left end clamps the position and turns the pet around.
    /// </summary>
    [Fact]
    public void Step_ReachesLeftEnd_ClampsAndTurnsRight()
    {
        // Act
        var (x, direction) = WalkMovement.Step(10, -1, 60, TimeSpan.FromSeconds(1), 0, 1000);

        // Assert
        x.Should().Be(0);
        direction.Should().Be(1);
    }

    /// <summary>
    /// A zero time step does not move the pet.
    /// </summary>
    [Fact]
    public void Step_ZeroDelta_DoesNotMove()
    {
        // Act
        var (x, direction) = WalkMovement.Step(100, 1, 60, TimeSpan.Zero, 0, 1000);

        // Assert
        x.Should().Be(100);
        direction.Should().Be(1);
    }

    /// <summary>
    /// If the range is narrower than nothing (max below min) the pet stays at the left end instead of throwing.
    /// </summary>
    [Fact]
    public void Step_MaxBelowMin_StaysAtMin()
    {
        // Act
        var (x, _) = WalkMovement.Step(50, 1, 60, TimeSpan.FromSeconds(1), 100, 50);

        // Assert
        x.Should().Be(100);
    }

    /// <summary>
    /// Any non-positive direction is treated as walking left; any positive one as walking right.
    /// </summary>
    [Theory]
    [InlineData(-5, 70)]
    [InlineData(5, 130)]
    public void Step_NonUnitDirection_UsesSignOnly(int direction, double expectedX)
    {
        // Act
        var (x, _) = WalkMovement.Step(100, direction, 60, TimeSpan.FromSeconds(0.5), 0, 1000);

        // Assert
        x.Should().BeApproximately(expectedX, 0.001);
    }
}
