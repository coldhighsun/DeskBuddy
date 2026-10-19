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

    /// <summary>
    /// The time before a stroll is a real range: not negative, and the shortest is not above the longest, as a random
    /// time cannot be drawn from a range the wrong way round.
    /// </summary>
    [Fact]
    public void StrollInterval_Default_IsAValidRange()
    {
        // Arrange & Act
        var options = new PetBehaviorOptions();

        // Assert
        options.StrollIntervalMin.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        options.StrollIntervalMax.Should().BeGreaterThanOrEqualTo(options.StrollIntervalMin);
    }

    /// <summary>
    /// Strolls are on until the settings switch them off, so a pet that is never given the user's choice still moves.
    /// </summary>
    [Fact]
    public void StrollEnabled_Default_IsOn()
    {
        // Arrange & Act
        var options = new PetBehaviorOptions();

        // Assert
        options.StrollEnabled.Should().BeTrue();
    }
}
