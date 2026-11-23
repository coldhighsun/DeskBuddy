using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Tests.Interop;

/// <summary>
/// Tests for <see cref="WindowMover"/>.
/// </summary>
public sealed partial class WindowMoverTests
{
    /// <summary>
    /// Index of the extended window style for <see cref="GetWindowLongPtr"/>.
    /// </summary>
    private const int GwlExStyle = -20;

    /// <summary>
    /// <c>WS_EX_TOPMOST</c>: the window is in the topmost band.
    /// </summary>
    private const nint WsExTopmost = 0x00000008;

    /// <summary>
    /// A process id no window belongs to, so the test windows count as windows of another program.
    /// </summary>
    private const int NoSuchProcessId = -1;

    /// <summary>
    /// Reads a window long.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="index">Which value to read.</param>
    /// <returns>The value.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    /// <summary>
    /// A window that is not topmost is put in the topmost band by the raise.
    /// </summary>
    [Fact]
    public void RaiseToTopmost_WindowNotTopmost_BecomesTopmost()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var window = new Window { Width = 50, Height = 50, ShowInTaskbar = false, Topmost = false };
            var handle = new WindowInteropHelper(window).EnsureHandle();
            (GetWindowLongPtr(handle, GwlExStyle) & WsExTopmost).Should().Be(0);

            // Act
            WindowMover.RaiseToTopmost(window);

            // Assert
            (GetWindowLongPtr(handle, GwlExStyle) & WsExTopmost).Should().Be(WsExTopmost);
            window.Close();
        });
    }

    /// <summary>
    /// Creates a window that is shown far off the screen without taking the focus; the window manager only keeps the
    /// z-order of windows that are shown.
    /// </summary>
    /// <returns>The window.</returns>
    private static Window CreateHiddenWindow(double left = -20000)
    {
        var window = new Window
        {
            Width = 50,
            Height = 50,
            Left = left,
            Top = -20000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            Topmost = true,
        };
        window.Show();
        return window;
    }

    /// <summary>
    /// A window raised after another one lies above it and covers it.
    /// </summary>
    [Fact]
    public void IsCovered_AnotherWindowRaisedAfterwards_IsTrue()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var lower = CreateHiddenWindow();
            var upper = CreateHiddenWindow();
            WindowMover.RaiseToTopmost(lower);
            WindowMover.RaiseToTopmost(upper);

            // Act
            var covered = WindowMover.IsCovered(lower, [], NoSuchProcessId);

            // Assert
            covered.Should().BeTrue();
            lower.Close();
            upper.Close();
        });
    }

    /// <summary>
    /// A window above that is listed as the program's own does not count as covering, but another one above it does.
    /// </summary>
    [Fact]
    public void IsCovered_OwnWindowAbove_IgnoresOnlyTheListedWindow()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var lower = CreateHiddenWindow();
            var own = CreateHiddenWindow();
            var upper = CreateHiddenWindow();
            WindowMover.RaiseToTopmost(lower);
            WindowMover.RaiseToTopmost(own);
            WindowMover.RaiseToTopmost(upper);

            // Act
            var coveredByUpper = WindowMover.IsCovered(lower, [own, null], NoSuchProcessId);

            // Assert
            coveredByUpper.Should().BeTrue();
            lower.Close();
            own.Close();
            upper.Close();
        });
    }

    /// <summary>
    /// Windows of this process are never counted, so they do not make the window look covered.
    /// </summary>
    [Fact]
    public void IsCovered_WindowOfThisProcessAbove_IsFalse()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var lower = CreateHiddenWindow();
            var upper = CreateHiddenWindow();
            WindowMover.RaiseToTopmost(lower);
            WindowMover.RaiseToTopmost(upper);

            // Act
            var covered = WindowMover.IsCovered(lower, [], Environment.ProcessId);

            // Assert
            covered.Should().BeFalse();
            lower.Close();
            upper.Close();
        });
    }

    /// <summary>
    /// A window above that does not overlap the window is not covering it.
    /// </summary>
    [Fact]
    public void IsCovered_WindowAboveDoesNotOverlap_IsFalse()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var lower = CreateHiddenWindow();
            var upper = CreateHiddenWindow(left: -30000);
            WindowMover.RaiseToTopmost(lower);
            WindowMover.RaiseToTopmost(upper);

            // Act
            var covered = WindowMover.IsCovered(lower, [], NoSuchProcessId);

            // Assert
            covered.Should().BeFalse();
            lower.Close();
            upper.Close();
        });
    }

    /// <summary>
    /// Rectangles that share area overlap.
    /// </summary>
    [Fact]
    public void Overlaps_RectanglesShareArea_IsTrue()
    {
        // Arrange
        var first = new PixelRect(0, 0, 10, 10);
        var second = new PixelRect(5, 5, 15, 15);

        // Act
        var overlaps = WindowMover.Overlaps(first, second);

        // Assert
        overlaps.Should().BeTrue();
    }

    /// <summary>
    /// Rectangles that only touch at an edge, or lie apart, do not overlap.
    /// </summary>
    [Theory]
    [InlineData(10, 0, 20, 10)]
    [InlineData(0, 10, 10, 20)]
    [InlineData(30, 30, 40, 40)]
    public void Overlaps_RectanglesTouchOrLieApart_IsFalse(int left, int top, int right, int bottom)
    {
        // Arrange
        var first = new PixelRect(0, 0, 10, 10);
        var second = new PixelRect(left, top, right, bottom);

        // Act
        var overlaps = WindowMover.Overlaps(first, second);

        // Assert
        overlaps.Should().BeFalse();
    }

    /// <summary>
    /// A step to another device pixel is a move, converted at the display scale.
    /// </summary>
    [Fact]
    public void TryGetMove_TargetInAnotherPixel_MovesInDevicePixels()
    {
        // Arrange
        // 150% scaling: 100 DIP is 150 px, and 102 DIP is 153 px.

        // Act
        var moved = WindowMover.TryGetMove(100, 50, 102, 50, 1.5, 1.5, out var x, out var y);

        // Assert
        moved.Should().BeTrue();
        x.Should().Be(153);
        y.Should().Be(75);
    }

    /// <summary>
    /// A step smaller than a pixel does not call the window manager.
    /// </summary>
    [Fact]
    public void TryGetMove_TargetInSamePixel_DoesNotMove()
    {
        // Arrange
        // At 100% scaling 100.2 and 100.4 DIP are both pixel 100.

        // Act
        var moved = WindowMover.TryGetMove(100.2, 50, 100.4, 50, 1, 1, out var x, out var y);

        // Assert
        moved.Should().BeFalse();
        x.Should().Be(100);
        y.Should().Be(50);
    }

    /// <summary>
    /// A change in only the vertical position still counts as a move.
    /// </summary>
    [Fact]
    public void TryGetMove_OnlyVerticalChange_Moves()
    {
        // Arrange and Act
        var moved = WindowMover.TryGetMove(100, 50, 100, 52, 1, 1, out _, out var y);

        // Assert
        moved.Should().BeTrue();
        y.Should().Be(52);
    }

    /// <summary>
    /// Positions on a monitor left of the primary one have negative coordinates and still convert correctly.
    /// </summary>
    [Fact]
    public void TryGetMove_NegativeCoordinates_RoundAwayFromZero()
    {
        // Arrange and Act
        var moved = WindowMover.TryGetMove(-100, 0, -100.5, 0, 1, 1, out var x, out _);

        // Assert
        moved.Should().BeTrue();
        x.Should().Be(-101);
    }
}
