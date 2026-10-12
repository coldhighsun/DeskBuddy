using AwesomeAssertions;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Movement;

namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="FreeFall"/>.
/// </summary>
public class FreeFallTests
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
    /// A drop from far above the resting place is a fall.
    /// </summary>
    [Fact]
    public void ShouldFall_HighAboveLanding_ReturnsTrue()
    {
        // Act
        var result = FreeFall.ShouldFall(dropY: 100, landingY: 600, minHeight: 160);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// A drop close to the resting place just settles.
    /// </summary>
    [Fact]
    public void ShouldFall_CloseToLanding_ReturnsFalse()
    {
        // Act
        var result = FreeFall.ShouldFall(dropY: 500, landingY: 600, minHeight: 160);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// A pet dropped below its resting place does not fall.
    /// </summary>
    [Fact]
    public void ShouldFall_BelowLanding_ReturnsFalse()
    {
        // Act
        var result = FreeFall.ShouldFall(dropY: 700, landingY: 600, minHeight: 160);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Without a canopy the pet speeds up under gravity.
    /// </summary>
    [Fact]
    public void Step_FromRest_Accelerates()
    {
        // Arrange
        var state = new FallState(0, 0, false, false);

        // Act
        var next = FreeFall.Step(state, 1000, Tick, _options);

        // Assert
        next.Velocity.Should().BeGreaterThan(0);
        next.Y.Should().BeGreaterThan(0);
        next.Opened.Should().BeFalse();
        next.Landed.Should().BeFalse();
    }

    /// <summary>
    /// Once fast enough the canopy opens.
    /// </summary>
    [Fact]
    public void Step_AtOpenSpeed_OpensCanopy()
    {
        // Arrange
        var state = new FallState(0, _options.FallOpenSpeed, false, false);

        // Act
        var next = FreeFall.Step(state, 1000, Tick, _options);

        // Assert
        next.Opened.Should().BeTrue();
    }

    /// <summary>
    /// With the canopy open the pet slows down to the terminal velocity and no further.
    /// </summary>
    [Fact]
    public void Step_CanopyOpen_SettlesAtTerminalVelocity()
    {
        // Arrange
        var state = new FallState(0, 600, true, false);

        // Act
        for (var i = 0; i < 100; i++)
        {
            state = FreeFall.Step(state, 100000, Tick, _options);
        }

        // Assert
        state.Velocity.Should().BeApproximately(_options.FallTerminalVelocity, 0.001);
    }

    /// <summary>
    /// The open canopy slows the pet gradually: one step takes off no more than the braking allows.
    /// </summary>
    [Fact]
    public void Step_CanopyJustOpened_BrakesGradually()
    {
        // Arrange
        var state = new FallState(0, _options.FallOpenSpeed, true, false);

        // Act
        var next = FreeFall.Step(state, 100000, Tick, _options);

        // Assert
        next.Velocity.Should().BeApproximately(_options.FallOpenSpeed - (_options.FallBrake * Tick.TotalSeconds), 0.001);
        next.Velocity.Should().BeGreaterThan(_options.FallTerminalVelocity);
    }

    /// <summary>
    /// The pet never goes below the landing place and reports landing.
    /// </summary>
    [Fact]
    public void Step_ReachesLanding_StopsAtLandingY()
    {
        // Arrange
        var state = new FallState(995, 300, true, false);

        // Act
        var next = FreeFall.Step(state, 1000, TimeSpan.FromSeconds(1), _options);

        // Assert
        next.Landed.Should().BeTrue();
        next.Y.Should().Be(1000);
        next.Velocity.Should().Be(0);
    }

    /// <summary>
    /// A whole fall ends in a landing.
    /// </summary>
    [Fact]
    public void Step_RepeatedFromHigh_EventuallyLandsWithCanopyOpened()
    {
        // Arrange
        var state = new FallState(0, 0, false, false);
        var steps = 0;

        // Act
        while (!state.Landed && steps++ < 10000)
        {
            state = FreeFall.Step(state, 900, Tick, _options);
        }

        // Assert
        state.Landed.Should().BeTrue();
        state.Opened.Should().BeTrue();
        state.Y.Should().Be(900);
    }
}
