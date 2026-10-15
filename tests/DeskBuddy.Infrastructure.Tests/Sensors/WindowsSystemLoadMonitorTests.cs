using DeskBuddy.Infrastructure.Sensors;

namespace DeskBuddy.Infrastructure.Tests.Sensors;

/// <summary>
/// Tests for <see cref="WindowsSystemLoadMonitor"/>.
/// </summary>
public class WindowsSystemLoadMonitorTests
{
    /// <summary>
    /// Fully busy CPU: all elapsed kernel+user time was non-idle.
    /// </summary>
    [Fact]
    public void ComputeCpuPercent_NoIdleTime_Returns100()
    {
        // Act: kernel advanced 600 and user 400, idle did not advance.
        var cpu = WindowsSystemLoadMonitor.ComputeCpuPercent(0, 0, 0, 0, 600, 400);

        // Assert
        cpu.Should().BeApproximately(100, 0.001);
    }

    /// <summary>
    /// Kernel time includes idle time, so idle must be subtracted from the total.
    /// </summary>
    [Fact]
    public void ComputeCpuPercent_HalfIdle_Returns50()
    {
        // Act: total 1000 (kernel 700 incl. idle 500, user 300) -> busy 500.
        var cpu = WindowsSystemLoadMonitor.ComputeCpuPercent(0, 0, 0, 500, 700, 300);

        // Assert
        cpu.Should().BeApproximately(50, 0.001);
    }

    /// <summary>
    /// Only the delta between the two readings counts, not the cumulative totals.
    /// </summary>
    [Fact]
    public void ComputeCpuPercent_UsesDeltaBetweenReadings()
    {
        // Act: earlier cumulative values are large; delta total 100, idle delta 75.
        var cpu = WindowsSystemLoadMonitor.ComputeCpuPercent(
            previousIdle: 1_000_000,
            previousKernel: 2_000_000,
            previousUser: 3_000_000,
            idle: 1_000_075,
            kernel: 2_000_080,
            user: 3_000_020);

        // Assert
        cpu.Should().BeApproximately(25, 0.001);
    }

    /// <summary>
    /// Two identical readings (no elapsed time) give zero instead of dividing by zero.
    /// </summary>
    [Fact]
    public void ComputeCpuPercent_NoElapsedTime_ReturnsZero()
    {
        // Act
        var cpu = WindowsSystemLoadMonitor.ComputeCpuPercent(5, 10, 10, 5, 10, 10);

        // Assert
        cpu.Should().Be(0);
    }

    /// <summary>
    /// Counter jitter that makes idle exceed total is clamped instead of going negative.
    /// </summary>
    [Fact]
    public void ComputeCpuPercent_IdleExceedsTotal_ClampsToZero()
    {
        // Act
        var cpu = WindowsSystemLoadMonitor.ComputeCpuPercent(0, 0, 0, 200, 100, 0);

        // Assert
        cpu.Should().Be(0);
    }

    /// <summary>
    /// The first real reading has no earlier baseline, so it reports zero CPU rather than garbage.
    /// </summary>
    [Fact]
    public void GetSnapshot_FirstCall_ReportsZeroCpuAndValidMemory()
    {
        // Arrange
        var monitor = new WindowsSystemLoadMonitor();

        // Act
        var snapshot = monitor.GetSnapshot();

        // Assert
        snapshot.CpuPercent.Should().Be(0);
        snapshot.MemoryPercent.Should().BeInRange(1, 100);
    }

    /// <summary>
    /// Subsequent readings from the real API stay within 0-100.
    /// </summary>
    [Fact]
    public async Task GetSnapshot_SecondCall_StaysWithinRange()
    {
        // Arrange
        var monitor = new WindowsSystemLoadMonitor();
        monitor.GetSnapshot();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // Act
        var snapshot = monitor.GetSnapshot();

        // Assert
        snapshot.CpuPercent.Should().BeInRange(0, 100);
        snapshot.MemoryPercent.Should().BeInRange(1, 100);
    }
}
