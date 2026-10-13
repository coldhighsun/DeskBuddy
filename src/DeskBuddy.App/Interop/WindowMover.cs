using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Moves a window the cheap way. Setting <see cref="Window.Left"/> and <see cref="Window.Top"/> costs a full
/// <c>SetWindowPos</c> each, with a repaint request; moving the pet around dozens of times a second that is the biggest
/// item in its CPU use. Here the window is moved once, in whole device pixels, and without apetg for a repaint — the
/// desktop compositor already holds the window's contents and just draws them somewhere else.
/// </summary>
public static partial class WindowMover
{
    /// <summary>
    /// <c>SWP_NOSIZE</c>: keep the current size.
    /// </summary>
    private const uint SwpNoSize = 0x0001;

    /// <summary>
    /// <c>SWP_NOZORDER</c>: keep the current position in the z-order.
    /// </summary>
    private const uint SwpNoZOrder = 0x0004;

    /// <summary>
    /// <c>SWP_NOREDRAW</c>: do not repaint; the compositor moves the existing picture.
    /// </summary>
    private const uint SwpNoRedraw = 0x0008;

    /// <summary>
    /// <c>SWP_NOACTIVATE</c>: do not take the focus from the user's application.
    /// </summary>
    private const uint SwpNoActivate = 0x0010;

    /// <summary>
    /// <c>SWP_NOOWNERZORDER</c>: do not change the z-order of the owner.
    /// </summary>
    private const uint SwpNoOwnerZOrder = 0x0200;

    /// <summary>
    /// Works out the device-pixel position a window has to be moved to, and whether that is different from where it is.
    /// </summary>
    /// <param name="currentLeft">
    /// The current left edge, in device-independent pixels.
    /// </param>
    /// <param name="currentTop">
    /// The current top edge, in device-independent pixels.
    /// </param>
    /// <param name="targetLeft">
    /// The wanted left edge, in device-independent pixels.
    /// </param>
    /// <param name="targetTop">
    /// The wanted top edge, in device-independent pixels.
    /// </param>
    /// <param name="scaleX">
    /// Device pixels per device-independent pixel, horizontally.
    /// </param>
    /// <param name="scaleY">
    /// Device pixels per device-independent pixel, vertically.
    /// </param>
    /// <param name="x">
    /// The wanted left edge in device pixels.
    /// </param>
    /// <param name="y">
    /// The wanted top edge in device pixels.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the target is a different device pixel than the current position; a move smaller than
    /// a pixel is not worth a call to the window manager.
    /// </returns>
    public static bool TryGetMove(
        double currentLeft,
        double currentTop,
        double targetLeft,
        double targetTop,
        double scaleX,
        double scaleY,
        out int x,
        out int y)
    {
        x = (int)Math.Round(targetLeft * scaleX, MidpointRounding.AwayFromZero);
        y = (int)Math.Round(targetTop * scaleY, MidpointRounding.AwayFromZero);

        var currentX = (int)Math.Round(currentLeft * scaleX, MidpointRounding.AwayFromZero);
        var currentY = (int)Math.Round(currentTop * scaleY, MidpointRounding.AwayFromZero);
        return x != currentX || y != currentY;
    }

    /// <summary>
    /// Moves a window to a position, in a single call and only if that changes its device pixel.
    /// </summary>
    /// <param name="window">
    /// The window to move; it must already have a handle.
    /// </param>
    /// <param name="left">
    /// The wanted left edge, in device-independent pixels.
    /// </param>
    /// <param name="top">
    /// The wanted top edge, in device-independent pixels.
    /// </param>
    public static void MoveTo(Window window, double left, double top)
    {
        // Compared with where the window really is: Window.Left and Window.Top are in the scaling the window had when it
        // was last moved, and can be out by the ratio of two scalings after it has moved onto another monitor.
        var dpi = VisualTreeHelper.GetDpi(window);
        var actual = MonitorHelper.GetWindowBoundsPixels(window);
        if (!TryGetMove(
            actual.Left / dpi.DpiScaleX,
            actual.Top / dpi.DpiScaleY,
            left,
            top,
            dpi.DpiScaleX,
            dpi.DpiScaleY,
            out var x,
            out var y))
        {
            return;
        }

        MoveToPixelsSettled(window, x, y);
    }

    /// <summary>
    /// Moves a window to a position given in device pixels, and makes sure it ends up there. Moving the window onto a
    /// monitor with another scaling makes Windows send a DPI change while it is being moved, and the window is then put
    /// where Windows suggests, which can be a long way from where it was moved to (a pet seems to leap ahead). The move
    /// is done again, now that the scaling has settled.
    /// </summary>
    /// <param name="window">
    /// The window to move; it must already have a handle.
    /// </param>
    /// <param name="x">
    /// The wanted left edge in device pixels.
    /// </param>
    /// <param name="y">
    /// The wanted top edge in device pixels.
    /// </param>
    public static void MoveToPixelsSettled(Window window, int x, int y)
    {
        var before = VisualTreeHelper.GetDpi(window);
        MoveToPixels(window, x, y);

        var after = VisualTreeHelper.GetDpi(window);
        if (after.DpiScaleX != before.DpiScaleX || after.DpiScaleY != before.DpiScaleY)
        {
            MoveToPixels(window, x, y);
        }
    }

    /// <summary>
    /// Moves a window to a position given in device pixels of the virtual screen. Unlike <see cref="Window.Left"/> this
    /// is exact on any monitor, whatever its scaling, and does not depend on which monitor the window thinks it is on.
    /// </summary>
    /// <param name="window">
    /// The window to move; it must already have a handle.
    /// </param>
    /// <param name="x">
    /// The wanted left edge in device pixels.
    /// </param>
    /// <param name="y">
    /// The wanted top edge in device pixels.
    /// </param>
    public static void MoveToPixels(Window window, int x, int y)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        SetWindowPos(
            handle,
            0,
            x,
            y,
            0,
            0,
            SwpNoSize | SwpNoZOrder | SwpNoRedraw | SwpNoActivate | SwpNoOwnerZOrder);
    }

    /// <summary>
    /// Moves two windows to positions given in device pixels in a single batch, so the window manager applies both
    /// together and they are drawn in the same frame.
    /// </summary>
    /// <param name="first">
    /// The first window to move.
    /// </param>
    /// <param name="firstX">
    /// The first window's wanted left edge in device pixels.
    /// </param>
    /// <param name="firstY">
    /// The first window's wanted top edge in device pixels.
    /// </param>
    /// <param name="second">
    /// The second window to move.
    /// </param>
    /// <param name="secondX">
    /// The second window's wanted left edge in device pixels.
    /// </param>
    /// <param name="secondY">
    /// The second window's wanted top edge in device pixels.
    /// </param>
    public static void MoveTogetherToPixels(Window first, int firstX, int firstY, Window second, int secondX, int secondY)
    {
        const uint flags = SwpNoSize | SwpNoZOrder | SwpNoRedraw | SwpNoActivate | SwpNoOwnerZOrder;
        var firstHandle = new WindowInteropHelper(first).EnsureHandle();
        var secondHandle = new WindowInteropHelper(second).EnsureHandle();

        var batch = BeginDeferWindowPos(2);
        if (batch != 0)
        {
            batch = DeferWindowPos(batch, firstHandle, 0, firstX, firstY, 0, 0, flags);
        }

        if (batch != 0)
        {
            batch = DeferWindowPos(batch, secondHandle, 0, secondX, secondY, 0, 0, flags);
        }

        if (batch != 0 && EndDeferWindowPos(batch))
        {
            return;
        }

        // The batch could not be built or applied (a failed call frees it); move the windows one by one instead.
        MoveToPixels(first, firstX, firstY);
        MoveToPixels(second, secondX, secondY);
    }

    /// <summary>
    /// Starts a batch of window moves.
    /// </summary>
    /// <param name="windowCount">
    /// How many windows the batch is sized for.
    /// </param>
    /// <returns>
    /// The batch handle, or zero on failure.
    /// </returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint BeginDeferWindowPos(int windowCount);

    /// <summary>
    /// Adds one window move to a batch.
    /// </summary>
    /// <param name="batch">
    /// The batch handle.
    /// </param>
    /// <param name="hwnd">
    /// Window handle.
    /// </param>
    /// <param name="insertAfter">
    /// Z-order neighbour; unused with <c>SWP_NOZORDER</c>.
    /// </param>
    /// <param name="x">
    /// New left edge in device pixels.
    /// </param>
    /// <param name="y">
    /// New top edge in device pixels.
    /// </param>
    /// <param name="width">
    /// New width; unused with <c>SWP_NOSIZE</c>.
    /// </param>
    /// <param name="height">
    /// New height; unused with <c>SWP_NOSIZE</c>.
    /// </param>
    /// <param name="flags">
    /// <c>SWP_*</c> flags.
    /// </param>
    /// <returns>
    /// The handle of the batch to use from now on, or zero on failure.
    /// </returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint DeferWindowPos(nint batch, nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    /// <summary>
    /// Applies a batch of window moves.
    /// </summary>
    /// <param name="batch">
    /// The batch handle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndDeferWindowPos(nint batch);

    /// <summary>
    /// Changes the position of a window.
    /// </summary>
    /// <param name="hwnd">
    /// Window handle.
    /// </param>
    /// <param name="insertAfter">
    /// Z-order neighbour; unused with <c>SWP_NOZORDER</c>.
    /// </param>
    /// <param name="x">
    /// New left edge in device pixels.
    /// </param>
    /// <param name="y">
    /// New top edge in device pixels.
    /// </param>
    /// <param name="width">
    /// New width; unused with <c>SWP_NOSIZE</c>.
    /// </param>
    /// <param name="height">
    /// New height; unused with <c>SWP_NOSIZE</c>.
    /// </param>
    /// <param name="flags">
    /// <c>SWP_*</c> flags.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
}
