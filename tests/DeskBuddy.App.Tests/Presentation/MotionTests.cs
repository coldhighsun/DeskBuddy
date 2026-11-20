namespace DeskBuddy.App.Tests.Presentation;

/// <summary>
/// Tests for <see cref="Motion"/>.
/// </summary>
public sealed class MotionTests
{
    /// <summary>
    /// With animations allowed, an animation keeps its duration.
    /// </summary>
    [Fact]
    public void Resolve_AnimationsEnabled_KeepsDuration()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(200);

        // Act
        var resolved = Motion.Resolve(duration, animationsEnabled: true);

        // Assert
        resolved.Should().Be(duration);
    }

    /// <summary>
    /// With animations turned off in Windows, an animation takes no time, so nothing moves.
    /// </summary>
    [Fact]
    public void Resolve_AnimationsDisabled_IsZero()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(200);

        // Act
        var resolved = Motion.Resolve(duration, animationsEnabled: false);

        // Assert
        resolved.Should().Be(TimeSpan.Zero);
    }
}
