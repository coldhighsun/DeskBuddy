using AwesomeAssertions;
using DeskBuddy.Domain.Movement;

namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="HopArc"/>.
/// </summary>
public class HopArcTests
{
    /// <summary>
    /// How long the hops in the tests last.
    /// </summary>
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// At the start of a hop the pet is still on the ground.
    /// </summary>
    [Fact]
    public void Offset_AtStart_IsZero()
    {
        // Act
        var result = HopArc.Offset(TimeSpan.Zero, Duration, petHeight: 64, heightRatio: 0.15);

        // Assert
        result.Should().Be(0);
    }

    /// <summary>
    /// Half way through, the pet is at the top, as high as the ratio of its height says.
    /// </summary>
    [Fact]
    public void Offset_AtMiddle_IsHeightTimesRatio()
    {
        // Act
        var result = HopArc.Offset(Duration / 2, Duration, petHeight: 64, heightRatio: 0.25);

        // Assert
        result.Should().BeApproximately(16, 1e-9);
    }

    /// <summary>
    /// At the end of the hop, and after it, the pet is back on the ground.
    /// </summary>
    [Theory]
    [InlineData(600)]
    [InlineData(900)]
    public void Offset_AtOrAfterEnd_IsZero(int milliseconds)
    {
        // Act
        var result = HopArc.Offset(TimeSpan.FromMilliseconds(milliseconds), Duration, petHeight: 64, heightRatio: 0.15);

        // Assert
        result.Should().Be(0);
    }

    /// <summary>
    /// The pet rises to the middle of the hop and comes down again the same way.
    /// </summary>
    [Fact]
    public void Offset_BothSidesOfTheMiddle_AreMirrored()
    {
        // Act
        var rising = HopArc.Offset(TimeSpan.FromMilliseconds(150), Duration, petHeight: 64, heightRatio: 0.15);
        var falling = HopArc.Offset(TimeSpan.FromMilliseconds(450), Duration, petHeight: 64, heightRatio: 0.15);

        // Assert
        rising.Should().BeGreaterThan(0);
        falling.Should().BeApproximately(rising, 1e-9);
    }

    /// <summary>
    /// A bigger pet hops higher, in proportion.
    /// </summary>
    [Fact]
    public void Offset_DoubleHeight_DoublesTheHop()
    {
        // Act
        var small = HopArc.Offset(Duration / 2, Duration, petHeight: 64, heightRatio: 0.15);
        var big = HopArc.Offset(Duration / 2, Duration, petHeight: 128, heightRatio: 0.15);

        // Assert
        big.Should().BeApproximately(small * 2, 1e-9);
    }

    /// <summary>
    /// A hop that lasts no time does not move the pet.
    /// </summary>
    [Fact]
    public void Offset_NoDuration_IsZero()
    {
        // Act
        var result = HopArc.Offset(TimeSpan.FromMilliseconds(10), TimeSpan.Zero, petHeight: 64, heightRatio: 0.15);

        // Assert
        result.Should().Be(0);
    }

    /// <summary>
    /// A hop is over exactly when its whole duration has passed, and not before.
    /// </summary>
    /// <param name="milliseconds">
    /// The time since the hop began.
    /// </param>
    /// <param name="expected">
    /// Whether the hop is over.
    /// </param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(599, false)]
    [InlineData(600, true)]
    [InlineData(1500, true)]
    public void IsOver_ElapsedAgainstDuration_MatchesExpectation(int milliseconds, bool expected)
    {
        // Act
        var result = HopArc.IsOver(TimeSpan.FromMilliseconds(milliseconds), Duration);

        // Assert
        result.Should().Be(expected);
    }
}
