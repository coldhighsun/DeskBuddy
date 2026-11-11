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
    /// The idle and walk ranges are real ranges (not negative, shortest not above longest), as random times cannot be
    /// drawn from a range the wrong way round.
    /// </summary>
    [Fact]
    public void IdleAndWalkTimes_Default_AreValidRanges()
    {
        // Arrange & Act
        var options = new PetBehaviorOptions();

        // Assert
        options.IdleDwellMin.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        options.IdleDwellMax.Should().BeGreaterThanOrEqualTo(options.IdleDwellMin);
        options.WalkDurationMin.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        options.WalkDurationMax.Should().BeGreaterThanOrEqualTo(options.WalkDurationMin);
    }

    /// <summary>
    /// A pet that is free to roam spends about four fifths of its time walking: the share of the average walk in an
    /// average stand-and-walk cycle is about 0.8.
    /// </summary>
    [Fact]
    public void WalkShare_Default_IsAboutFourFifths()
    {
        // Arrange & Act
        var options = new PetBehaviorOptions();
        var averageIdle = (options.IdleDwellMin + options.IdleDwellMax) / 2;
        var averageWalk = (options.WalkDurationMin + options.WalkDurationMax) / 2;
        var walkShare = averageWalk / (averageWalk + averageIdle);

        // Assert
        walkShare.Should().BeApproximately(0.8, 0.02);
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
