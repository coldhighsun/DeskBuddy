using AwesomeAssertions;
using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Tests.Behavior;

/// <summary>
/// Tests for <see cref="RandomExtensions"/>.
/// </summary>
public class RandomExtensionsTests
{
    /// <summary>
    /// A random duration lies within the bounds.
    /// </summary>
    [Fact]
    public void NextTimeSpan_Range_StaysWithinBounds()
    {
        // Arrange
        var random = new Random(42);
        var min = TimeSpan.FromSeconds(2);
        var max = TimeSpan.FromSeconds(5);

        // Act
        var values = Enumerable.Range(0, 200).Select(_ => random.NextTimeSpan(min, max)).ToList();

        // Assert
        values.Should().OnlyContain(value => value >= min && value < max);
    }

    /// <summary>
    /// An empty or inverted range gives its lower bound.
    /// </summary>
    [Fact]
    public void NextTimeSpan_UpperNotGreater_ReturnsMin()
    {
        // Act
        var value = new Random(1).NextTimeSpan(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));

        // Assert
        value.Should().Be(TimeSpan.FromSeconds(3));
    }
}
