using System.Runtime.InteropServices;
using AwesomeAssertions;
using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="MonitorHelper"/>.
/// </summary>
public class MonitorHelperTests
{
    /// <summary>
    /// The <c>GetSystemMetrics</c> index for the left edge of the virtual screen.
    /// </summary>
    private const int VirtualScreenLeft = 76;

    /// <summary>
    /// The <c>GetSystemMetrics</c> index for the top edge of the virtual screen.
    /// </summary>
    private const int VirtualScreenTop = 77;

    /// <summary>
    /// The <c>GetSystemMetrics</c> index for the width of the virtual screen.
    /// </summary>
    private const int VirtualScreenWidth = 78;

    /// <summary>
    /// The <c>GetSystemMetrics</c> index for the height of the virtual screen.
    /// </summary>
    private const int VirtualScreenHeight = 79;

    /// <summary>
    /// Every monitor listed has a work area with a size inside the virtual screen, and a scaling.
    /// </summary>
    [Fact]
    public void GetAllMonitors_Called_ListsWellFormedMonitorsInsideVirtualScreen()
    {
        // Arrange
        var (left, top, right, bottom) = GetVirtualScreen();

        // Act
        var monitors = MonitorHelper.GetAllMonitors();

        // Assert
        monitors.Should().OnlyContain(monitor =>
            monitor.WorkArea.Right > monitor.WorkArea.Left
            && monitor.WorkArea.Bottom > monitor.WorkArea.Top
            && monitor.WorkArea.Left >= left
            && monitor.WorkArea.Top >= top
            && monitor.WorkArea.Right <= right
            && monitor.WorkArea.Bottom <= bottom
            && monitor.Scale >= 1);
    }

    /// <summary>
    /// The list is empty exactly when the machine has no display: a monitor is not missed, and none is made up.
    /// </summary>
    [Fact]
    public void GetAllMonitors_Called_IsEmptyOnlyWithoutDisplay()
    {
        // Arrange
        var (left, top, right, bottom) = GetVirtualScreen();

        // Act
        var monitors = MonitorHelper.GetAllMonitors();

        // Assert
        monitors.Any().Should().Be(right > left && bottom > top);
    }

    /// <summary>
    /// Every call lists the monitors afresh: the result is not shared between calls.
    /// </summary>
    [Fact]
    public void GetAllMonitors_CalledTwice_ReturnsSameMonitorsInSeparateLists()
    {
        // Act
        var first = MonitorHelper.GetAllMonitors();
        var second = MonitorHelper.GetAllMonitors();

        // Assert
        second.Should().Equal(first);
        second.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// Reads the bounds of the virtual screen, the rectangle that holds all monitors.
    /// </summary>
    /// <returns>
    /// The left, top, right and bottom edge, in device pixels; an empty rectangle if there is no display.
    /// </returns>
    private static (int Left, int Top, int Right, int Bottom) GetVirtualScreen()
    {
        var left = GetSystemMetrics(VirtualScreenLeft);
        var top = GetSystemMetrics(VirtualScreenTop);
        return (left, top, left + GetSystemMetrics(VirtualScreenWidth), top + GetSystemMetrics(VirtualScreenHeight));
    }

    /// <summary>
    /// Reads a system metric.
    /// </summary>
    /// <param name="index">
    /// The metric to read.
    /// </param>
    /// <returns>
    /// The value of the metric.
    /// </returns>
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
