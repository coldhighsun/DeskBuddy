using AwesomeAssertions;
using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Tests.Behavior;

/// <summary>
/// Tests for the defaults of <see cref="PetBehaviorOptions"/> that the movement of the pet depends on.
/// </summary>
public class PetBehaviorOptionsTests
{
    /// <summary>
    /// The speed at which the pet changes height between monitors is positive, as the easing of the height cannot work
    /// with a speed of nothing.
    /// </summary>
    [Fact]
    public void VerticalEaseSpeed_Default_IsPositive()
    {
        // Arrange & Act
        var options = new PetBehaviorOptions();

        // Assert
        options.VerticalEaseSpeed.Should().BePositive();
    }

    /// <summary>
    /// The tolerance for the height is not negative, and smaller than the least height at which a pet falls, so that a
    /// difference worth a fall is never taken for none.
    /// </summary>
    [Fact]
    public void VerticalEaseTolerance_Default_IsNotNegativeAndBelowFallHeight()
    {
        // Arrange & Act
        var options = new PetBehaviorOptions();

        // Assert
        options.VerticalEaseTolerance.Should().BeGreaterThanOrEqualTo(0);
        options.VerticalEaseTolerance.Should().BeLessThan(options.FallMinHeight);
    }
}
