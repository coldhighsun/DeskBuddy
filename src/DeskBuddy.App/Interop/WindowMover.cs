using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Moves a window the cheap way. Setting <see cref="Window.Left"/> and <see cref="Window.Top"/> costs a full
/// <c>SetWindowPos</c> each, with a repaint request; moving the pet around dozens of times a second that is the biggest
/// item in its CPU use. Here the window is moved once, in whole device pixels, and without asking for a repaint — the
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
        var dpi = VisualTreeHelper.GetDpi(window);
        if (!TryGetMove(window.Left, window.Top, left, top, dpi.DpiScaleX, dpi.DpiScaleY, out var x, out var y))
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
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
