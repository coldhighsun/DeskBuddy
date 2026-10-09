using AwesomeAssertions;
using DeskBuddy.Infrastructure.Sensors;

namespace DeskBuddy.Infrastructure.Tests.Sensors;

/// <summary>
/// Tests for <see cref="WindowsFullscreenDetector"/>.
/// </summary>
public class WindowsFullscreenDetectorTests
{
    /// <summary>
    /// States that mean the user must not be disturbed.
    /// </summary>
    /// <param name="state">
    /// A <c>QUERY_USER_NOTIFICATION_STATE</c> value.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void IsSuppressing_DoNotDisturbStates_ReturnsTrue(int state)
    {
        // Act
        var result = WindowsFullscreenDetector.IsSuppressing(state);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// States where notices are fine, plus values the shell never reports.
    /// </summary>
    /// <param name="state">
    /// A <c>QUERY_USER_NOTIFICATION_STATE</c> value or an out-of-range value.
    /// </param>
    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void IsSuppressing_OtherStates_ReturnsFalse(int state)
    {
        // Act
        var result = WindowsFullscreenDetector.IsSuppressing(state);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// The real shell call succeeds without throwing.
    /// </summary>
    [Fact]
    public void IsDoNotDisturb_RealApi_DoesNotThrow()
    {
        // Arrange
        var detector = new WindowsFullscreenDetector();

        // Act
        var act = () => detector.IsDoNotDisturb();

        // Assert
        act.Should().NotThrow();
    }
}
