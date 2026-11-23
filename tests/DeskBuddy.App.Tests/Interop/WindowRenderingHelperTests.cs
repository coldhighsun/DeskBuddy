using System.Windows;
using System.Windows.Interop;
using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests.Interop;

/// <summary>
/// Tests for <see cref="WindowRenderingHelper"/>, on windows that are given a handle but never shown.
/// </summary>
public sealed class WindowRenderingHelperTests
{
    /// <summary>
    /// A window that gets the property before it has a handle draws on the CPU once it has one.
    /// </summary>
    [Fact]
    public void UseSoftwareRendering_SetBeforeHandle_RendersInSoftware()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var window = new Window();
            WindowRenderingHelper.SetUseSoftwareRendering(window, true);

            // Act
            var handle = new WindowInteropHelper(window).EnsureHandle();

            // Assert
            RenderModeOf(handle).Should().Be(RenderMode.SoftwareOnly);
            window.Close();
        });
    }

    /// <summary>
    /// A window that gets the property after it has a handle draws on the CPU at once.
    /// </summary>
    [Fact]
    public void UseSoftwareRendering_SetAfterHandle_RendersInSoftware()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var window = new Window();
            var handle = new WindowInteropHelper(window).EnsureHandle();

            // Act
            WindowRenderingHelper.SetUseSoftwareRendering(window, true);

            // Assert
            RenderModeOf(handle).Should().Be(RenderMode.SoftwareOnly);
            window.Close();
        });
    }

    /// <summary>
    /// Switching the property off goes back to the default rendering.
    /// </summary>
    [Fact]
    public void UseSoftwareRendering_SwitchedOff_GoesBackToDefault()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var window = new Window();
            var handle = new WindowInteropHelper(window).EnsureHandle();
            WindowRenderingHelper.SetUseSoftwareRendering(window, true);

            // Act
            WindowRenderingHelper.SetUseSoftwareRendering(window, false);

            // Assert
            RenderModeOf(handle).Should().Be(RenderMode.Default);
            window.Close();
        });
    }

    /// <summary>
    /// A window that does not use the property keeps the default rendering, as the pet's windows must.
    /// </summary>
    [Fact]
    public void UseSoftwareRendering_NotSet_KeepsDefault()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var window = new Window();

            // Act
            var handle = new WindowInteropHelper(window).EnsureHandle();

            // Assert
            WindowRenderingHelper.GetUseSoftwareRendering(window).Should().BeFalse();
            RenderModeOf(handle).Should().Be(RenderMode.Default);
            window.Close();
        });
    }

    /// <summary>
    /// Gets how a window is drawn.
    /// </summary>
    /// <param name="handle">The handle of the window.</param>
    /// <returns>The render mode of its render target.</returns>
    private static RenderMode RenderModeOf(nint handle)
    {
        return HwndSource.FromHwnd(handle)!.CompositionTarget!.RenderMode;
    }
}
