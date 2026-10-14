using AwesomeAssertions;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.Domain.Tests.Pets;

/// <summary>
/// Tests for <see cref="PetAnimation"/>.
/// </summary>
public class PetAnimationTests
{
    /// <summary>
    /// One run through the animation takes its frames times the time each frame stays on screen.
    /// </summary>
    [Theory]
    [InlineData(8, 5, 625)]
    [InlineData(4, 1, 250)]
    [InlineData(2, 4, 2000)]
    public void Duration_FramesAndFps_IsFramesTimesFrameDuration(double fps, int frames, int expectedMilliseconds)
    {
        // Arrange
        var animation = new PetAnimation(fps, [.. Enumerable.Range(0, frames).Select(index => $"frame_{index}.png")]);

        // Act
        var duration = animation.Duration;

        // Assert
        duration.Should().Be(TimeSpan.FromMilliseconds(expectedMilliseconds));
    }
}
