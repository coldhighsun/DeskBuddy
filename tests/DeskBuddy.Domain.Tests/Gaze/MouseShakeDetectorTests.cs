using AwesomeAssertions;
using DeskBuddy.Domain.Gaze;

namespace DeskBuddy.Domain.Tests.Gaze;

/// <summary>
/// Tests for <see cref="MouseShakeDetector"/>.
/// </summary>
public class MouseShakeDetectorTests
{
    /// <summary>
    /// The object under test, with the usual values.
    /// </summary>
    private readonly MouseShakeDetector _detector = new();

    /// <summary>
    /// Wide swings back and forth in quick succession are a shake.
    /// </summary>
    [Fact]
    public void Update_QuickWideSwings_DetectsShake()
    {
        // Act
        var result = Feed(0, 150, 0, 150, 0, 150, 0);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Moving the mouse across the screen once is not a shake.
    /// </summary>
    [Fact]
    public void Update_OneSweep_NoShake()
    {
        // Act
        var result = Feed(0, 300, 600, 900, 1200, 1500, 1800);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Small wobbles are not a shake.
    /// </summary>
    [Fact]
    public void Update_SmallWobbles_NoShake()
    {
        // Act
        var result = Feed(0, 20, 0, 20, 0, 20, 0, 20, 0, 20);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Swings that are spread over too long a time are not a shake.
    /// </summary>
    [Fact]
    public void Update_SlowSwings_NoShake()
    {
        // Arrange
        var detected = false;
        var positions = new[] { 0, 150, 0, 150, 0, 150, 0 };

        // Act
        for (var i = 0; i < positions.Length; i++)
        {
            detected |= _detector.Update(TimeSpan.FromSeconds(i), positions[i]);
        }

        // Assert
        detected.Should().BeFalse();
    }

    /// <summary>
    /// After a shake the detector starts over, so one more swing is not a second shake.
    /// </summary>
    [Fact]
    public void Update_AfterShake_StartsOver()
    {
        // Arrange
        Feed(0, 150, 0, 150, 0, 150, 0);

        // Act
        var result = _detector.Update(TimeSpan.FromMilliseconds(800), 150);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Feeds positions 100 milliseconds apart.
    /// </summary>
    /// <param name="positions">
    /// The horizontal positions.
    /// </param>
    /// <returns>
    /// Whether any of them completed a shake.
    /// </returns>
    private bool Feed(params int[] positions)
    {
        var detected = false;
        for (var i = 0; i < positions.Length; i++)
        {
            detected |= _detector.Update(TimeSpan.FromMilliseconds(100 * i), positions[i]);
        }

        return detected;
    }
}
