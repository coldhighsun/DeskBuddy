namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="ThrowMotion"/> and the upward part of a throw in <see cref="FreeFall"/>.
/// </summary>
public class ThrowMotionTests
{
    /// <summary>
    /// The step used by the tests.
    /// </summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(33);

    /// <summary>
    /// The tuning used by the tests.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// A thrown pet moves in the direction it was thrown, and the air slows it down.
    /// </summary>
    [Fact]
    public void Step_ThrownRight_MovesRightAndSlowsDown()
    {
        // Act
        var next = ThrowMotion.Step(new ThrowState(100, 1000), 0, 2000, Tick, _options);

        // Assert
        next.X.Should().BeGreaterThan(100);
        next.Velocity.Should().BeInRange(1, 999.999);
    }

    /// <summary>
    /// A pet thrown into the end of the screen stops there and bounces back, slower.
    /// </summary>
    [Fact]
    public void Step_HitsRightEnd_BouncesBackSlower()
    {
        // Act
        var next = ThrowMotion.Step(new ThrowState(1990, 1000), 0, 2000, Tick, _options);

        // Assert
        next.X.Should().Be(2000);
        next.Velocity.Should().BeNegative();
        Math.Abs(next.Velocity).Should().BeLessThan(1000 * _options.ThrowBounce + 0.001);
    }

    /// <summary>
    /// The left end bounces a pet back to the right.
    /// </summary>
    [Fact]
    public void Step_HitsLeftEnd_BouncesBackRight()
    {
        // Act
        var next = ThrowMotion.Step(new ThrowState(10, -1000), 0, 2000, Tick, _options);

        // Assert
        next.X.Should().Be(0);
        next.Velocity.Should().BePositive();
    }

    /// <summary>
    /// Once the air has nearly stopped it, the pet no longer drifts.
    /// </summary>
    [Fact]
    public void Step_AlmostStill_Stops()
    {
        // Act
        var next = ThrowMotion.Step(new ThrowState(500, 3), 0, 2000, Tick, _options);

        // Assert
        next.Should().Be(new ThrowState(500, 0));
    }

    /// <summary>
    /// A pet thrown upwards rises, slows down under gravity and then falls back to the ground.
    /// </summary>
    [Fact]
    public void FreeFallStep_ThrownUp_RisesThenLands()
    {
        // Arrange
        var state = new FallState(500, -800, false, false);
        var highest = state.Y;

        // Act
        for (var i = 0; i < 300 && !state.Landed; i++)
        {
            state = FreeFall.Step(state, 600, Tick, _options, ceilingY: 0);
            highest = Math.Min(highest, state.Y);
        }

        // Assert
        highest.Should().BeLessThan(500);
        state.Landed.Should().BeTrue();
        state.Y.Should().Be(600);
    }

    /// <summary>
    /// A pet thrown upwards stops at the top of the screen instead of leaving it.
    /// </summary>
    [Fact]
    public void FreeFallStep_ThrownPastTheTop_StopsAtTheCeiling()
    {
        // Act
        var next = FreeFall.Step(new FallState(10, -2000, false, false), 600, Tick, _options, ceilingY: 0);

        // Assert
        next.Y.Should().Be(0);
        next.Velocity.Should().BeGreaterThanOrEqualTo(0);
    }
}
