using DeskBuddy.Infrastructure.Sensors;

namespace DeskBuddy.Infrastructure.Tests.Sensors;

/// <summary>
/// Tests for <see cref="Win32IdleDetector"/>.
/// </summary>
public class Win32IdleDetectorTests
{
    /// <summary>
    /// The idle time is the difference between the two tick counts.
    /// </summary>
    [Fact]
    public void ComputeIdle_NormalTicks_ReturnsDifference()
    {
        // Act
        var idle = Win32IdleDetector.ComputeIdle(10_000, 7_500);

        // Assert
        idle.Should().Be(TimeSpan.FromMilliseconds(2_500));
    }

    /// <summary>
    /// The 32-bit tick counter wrapping around between the last input and now still yields the right difference.
    /// </summary>
    [Fact]
    public void ComputeIdle_CounterWrappedAround_ReturnsDifference()
    {
        // Act
        var idle = Win32IdleDetector.ComputeIdle(500, uint.MaxValue - 499);

        // Assert
        idle.Should().Be(TimeSpan.FromMilliseconds(1_000));
    }

    /// <summary>
    /// Input at this very tick means no idle time.
    /// </summary>
    [Fact]
    public void ComputeIdle_SameTick_ReturnsZero()
    {
        // Act
        var idle = Win32IdleDetector.ComputeIdle(1234, 1234);

        // Assert
        idle.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// The real Win32 call works and reports a non-negative duration.
    /// </summary>
    [Fact]
    public void GetIdleTime_RealApi_ReturnsNonNegative()
    {
        // Arrange
        var detector = new Win32IdleDetector();

        // Act
        var idle = detector.GetIdleTime();

        // Assert
        idle.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }
}
