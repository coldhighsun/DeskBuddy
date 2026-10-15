namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="VerticalEase"/>.
/// </summary>
public class VerticalEaseTests
{
    /// <summary>
    /// A target lower down on the screen is a drop, one higher up a rise, and one within the tolerance no change at all,
    /// so that the rounding of positions to device pixels is not taken for a different taskbar line.
    /// </summary>
    /// <param name="target">
    /// The height to go to.
    /// </param>
    /// <param name="expected">
    /// The expected kind of change.
    /// </param>
    [Theory]
    [InlineData(100, VerticalChange.None)]
    [InlineData(100.4, VerticalChange.None)]
    [InlineData(102, VerticalChange.None)]
    [InlineData(98, VerticalChange.None)]
    [InlineData(102.5, VerticalChange.Drop)]
    [InlineData(400, VerticalChange.Drop)]
    [InlineData(97.5, VerticalChange.Rise)]
    [InlineData(-50, VerticalChange.Rise)]
    public void Classify_Target_ReturnsKindOfChange(double target, VerticalChange expected)
    {
        // Arrange & Act
        var change = VerticalEase.Classify(100, target, 2);

        // Assert
        change.Should().Be(expected);
    }

    /// <summary>
    /// A target within reach is reached at once.
    /// </summary>
    /// <param name="current">
    /// The height now.
    /// </param>
    /// <param name="target">
    /// The height to go to.
    /// </param>
    [Theory]
    [InlineData(100, 110)]
    [InlineData(110, 100)]
    [InlineData(100, 100)]
    public void Approach_TargetWithinReach_ReturnsTarget(double current, double target)
    {
        // Arrange & Act
        var next = VerticalEase.Approach(current, target, 10);

        // Assert
        next.Should().Be(target);
    }

    /// <summary>
    /// A step that is not positive means no easing: the target is given at once, instead of the height standing still or
    /// moving away from it.
    /// </summary>
    /// <param name="maxStep">
    /// The largest step.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Approach_StepNotPositive_ReturnsTarget(double maxStep)
    {
        // Arrange & Act
        var next = VerticalEase.Approach(100, 500, maxStep);

        // Assert
        next.Should().Be(500);
    }

    /// <summary>
    /// A target out of reach is approached by exactly the largest step, down or up.
    /// </summary>
    /// <param name="target">
    /// The height to go to.
    /// </param>
    /// <param name="expected">
    /// The expected height after the step.
    /// </param>
    [Theory]
    [InlineData(500, 110)]
    [InlineData(-300, 90)]
    public void Approach_TargetOutOfReach_MovesByMaxStep(double target, double expected)
    {
        // Arrange & Act
        var next = VerticalEase.Approach(100, target, 10);

        // Assert
        next.Should().Be(expected);
    }
}
