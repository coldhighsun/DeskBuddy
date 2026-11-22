using System.Diagnostics;
using DeskBuddy.App.Diagnostics;

namespace DeskBuddy.App.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="PressureSample"/>.
/// </summary>
public sealed class PressureSampleTests
{
    /// <summary>
    /// One gigabyte, to write memory sizes readably.
    /// </summary>
    private const long Gb = 1024L * 1024 * 1024;

    /// <summary>
    /// Builds a sample.
    /// </summary>
    /// <param name="seconds">When it was taken, in seconds from an arbitrary start.</param>
    /// <param name="cpuMs">CPU time of the process so far, in milliseconds.</param>
    /// <param name="idleTicks">Idle time of the machine so far.</param>
    /// <param name="totalTicks">Total time of the machine so far.</param>
    /// <param name="faults">Page faults so far.</param>
    /// <param name="workingSet">Working set in bytes.</param>
    /// <returns>The sample.</returns>
    private static PressureSample Sample(
        double seconds,
        double cpuMs,
        ulong idleTicks,
        ulong totalTicks,
        uint faults,
        long workingSet)
    {
        return new PressureSample(
            DateTimeOffset.UnixEpoch.AddSeconds(seconds),
            TimeSpan.FromMilliseconds(cpuMs),
            workingSet,
            faults,
            idleTicks,
            totalTicks,
            4 * (ulong)Gb,
            16 * (ulong)Gb,
            90,
            ProcessPriorityClass.BelowNormal);
    }

    /// <summary>
    /// The line tells how much CPU the process got out of the time that passed, how busy the machine was and the page faults.
    /// </summary>
    [Fact]
    public void Describe_TwoSamples_ReportsDeltasAndMemory()
    {
        // Arrange
        var before = Sample(0, 1000, 0, 0, 100, 50 * 1024 * 1024);
        var after = Sample(3, 1012, 50, 1000, 4220, 40 * 1024 * 1024);

        // Act
        var text = PressureSample.Describe(before, after);

        // Assert
        Assert.Contains("process CPU 12 ms in 3000 ms", text);
        Assert.Contains("machine CPU 95 %", text);
        Assert.Contains("page faults +4120", text);
        Assert.Contains("working set 40 MB (-10 MB)", text);
        Assert.Contains("free RAM 4.0 of 16.0 GB", text);
        Assert.Contains("commit 90 %", text);
        Assert.Contains("priority BelowNormal", text);
    }

    /// <summary>
    /// Two samples with no machine time between them do not divide by zero.
    /// </summary>
    [Fact]
    public void Describe_NoMachineTimePassed_ReportsIdleMachine()
    {
        // Arrange
        var sample = Sample(0, 0, 10, 10, 0, 0);

        // Act
        var text = PressureSample.Describe(sample, sample);

        // Assert
        Assert.Contains("machine CPU 0 %", text);
    }

    /// <summary>
    /// A sample of the running test process has real numbers in it.
    /// </summary>
    [Fact]
    public void Take_RunningProcess_ReadsMemoryAndCpu()
    {
        // Arrange
        using var self = Process.GetCurrentProcess();

        // Act
        var sample = PressureSample.Take(self, DateTimeOffset.UtcNow);

        // Assert
        Assert.True(sample.WorkingSetBytes > 0);
        Assert.True(sample.TotalPhysicalBytes > 0);
        Assert.True(sample.PageFaults > 0);
        Assert.True(sample.ProcessCpu > TimeSpan.Zero);
    }
}
