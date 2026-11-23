using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DeskBuddy.App.Presentation;

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
    /// <c>SWP_NOMOVE</c>: keep the current position.
    /// </summary>
    private const uint SwpNoMove = 0x0002;

    /// <summary>
    /// <c>SWP_NOZORDER</c>: keep the current position in the z-order.
    /// </summary>
    private const uint SwpNoZOrder = 0x0004;

    /// <summary>
    /// <c>HWND_TOPMOST</c>: the insert-after value that puts a window at the top of the topmost band.
    /// </summary>
    private const nint HwndTopmost = -1;

    /// <summary>
    /// <c>DWMWA_CLOAKED</c>: the DWM attribute that says a window is hidden by the shell although it is "visible".
    /// </summary>
    private const int DwmaCloaked = 14;

    /// <summary>
    /// Capacity, in characters, of the buffer a window class name is read into; longer names are cut, which is fine as the
    /// names looked for are short.
    /// </summary>
    private const int ClassNameCapacity = 64;

    /// <summary>
    /// The window class of menus (<c>#32768</c>).
    /// </summary>
    private const string MenuClassName = "#32768";

    /// <summary>
    /// The window class of tooltips.
    /// </summary>
    private const string TooltipClassName = "tooltips_class32";

    /// <summary>
    /// <c>GW_HWNDPREV</c>: the window just above the given one in the z-order.
    /// </summary>
    private const uint GwHwndPrev = 3;

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
    /// <param name="currentLeft">The current left edge, in device-independent pixels.</param>
    /// <param name="currentTop">The current top edge, in device-independent pixels.</param>
    /// <param name="targetLeft">The wanted left edge, in device-independent pixels.</param>
    /// <param name="targetTop">The wanted top edge, in device-independent pixels.</param>
    /// <param name="scaleX">Device pixels per device-independent pixel, horizontally.</param>
    /// <param name="scaleY">Device pixels per device-independent pixel, vertically.</param>
    /// <param name="x">The wanted left edge in device pixels.</param>
    /// <param name="y">The wanted top edge in device pixels.</param>
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
    /// <param name="window">The window to move; it must already have a handle.</param>
    /// <param name="left">The wanted left edge, in device-independent pixels.</param>
    /// <param name="top">The wanted top edge, in device-independent pixels.</param>
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
    /// <param name="window">The window to move; it must already have a handle.</param>
    /// <param name="x">The wanted left edge in device pixels.</param>
    /// <param name="y">The wanted top edge in device pixels.</param>
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
    /// <param name="window">The window to move; it must already have a handle.</param>
    /// <param name="x">The wanted left edge in device pixels.</param>
    /// <param name="y">The wanted top edge in device pixels.</param>
    public static void MoveToPixels(Window window, int x, int y)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        var startedAt = Win32Timing.Start();
        SetWindowPos(
            handle,
            0,
            x,
            y,
            0,
            0,
            SwpNoSize | SwpNoZOrder | SwpNoRedraw | SwpNoActivate | SwpNoOwnerZOrder);
        Win32Timing.Stop("SetWindowPos", startedAt);
    }

    /// <summary>
    /// Puts a window just behind another one in the z-order, without moving, sizing or activating it, so what the other
    /// window draws covers it where they overlap.
    /// </summary>
    /// <param name="window">The window to put behind.</param>
    /// <param name="front">The window it goes behind.</param>
    public static void PlaceBehind(Window window, Window front)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        var frontHandle = new WindowInteropHelper(front).EnsureHandle();
        SetWindowPos(handle, frontHandle, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder);
    }

    /// <summary>
    /// Raises a window to the top of the topmost band without moving, sizing or activating it. Other always-on-top
    /// windows (the taskbar, a pinned program) can rise above a window that is already topmost, and nothing lifts it
    /// back by itself.
    /// </summary>
    /// <param name="window">The window to raise; it must already have a handle.</param>
    public static void RaiseToTopmost(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        var startedAt = Win32Timing.Start();
        SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder);
        Win32Timing.Stop("SetWindowPos", startedAt);
    }

    /// <summary>
    /// Tells whether a window of another program lies above a window in the z-order and hides part of it. Windows of the
    /// same program, windows that are not visible (or are cloaked), menus, tooltips and windows that do not overlap the
    /// given one are not counted, so a transient popup or a hidden helper window never makes the window look covered.
    /// </summary>
    /// <param name="window">The window to look at; it must already have a handle.</param>
    /// <param name="ownWindows">Windows that may lie above it without covering it (nulls are ignored).</param>
    /// <returns><see langword="true"/> if some other window is above it and overlaps it.</returns>
    public static bool IsCovered(Window window, IEnumerable<Window?> ownWindows)
    {
        return IsCovered(window, ownWindows, Environment.ProcessId);
    }

    /// <summary>
    /// Tells whether a window of another program lies above a window in the z-order and hides part of it, taking the id
    /// of "this program" from the caller (tests use an id no window has, to stand in for another program).
    /// </summary>
    /// <param name="window">The window to look at; it must already have a handle.</param>
    /// <param name="ownWindows">Windows that may lie above it without covering it (nulls are ignored).</param>
    /// <param name="ownProcessId">The id of the process whose windows never count as covering.</param>
    /// <returns><see langword="true"/> if some other window is above it and overlaps it.</returns>
    public static bool IsCovered(Window window, IEnumerable<Window?> ownWindows, int ownProcessId)
    {
        ArgumentNullException.ThrowIfNull(ownWindows);

        var handle = new WindowInteropHelper(window).EnsureHandle();
        if (!GetWindowRect(handle, out var native))
        {
            return false;
        }

        var bounds = native.ToPixelRect();

        var own = new HashSet<nint>();
        foreach (var ownWindow in ownWindows)
        {
            if (ownWindow is not null)
            {
                own.Add(new WindowInteropHelper(ownWindow).EnsureHandle());
            }
        }

        var current = GetWindow(handle, GwHwndPrev);
        while (current != 0)
        {
            if (!own.Contains(current) && CoversBounds(current, bounds, ownProcessId))
            {
                return true;
            }

            current = GetWindow(current, GwHwndPrev);
        }

        return false;
    }

    /// <summary>
    /// Tells whether a window above another one really hides part of it: it belongs to another program, is visible and not
    /// cloaked, is not a menu or a tooltip, and overlaps the given bounds.
    /// </summary>
    /// <param name="candidate">The window above.</param>
    /// <param name="bounds">The bounds of the window below, in device pixels.</param>
    /// <param name="ownProcessId">The id of this process, whose windows never count.</param>
    /// <returns><see langword="true"/> if the candidate covers part of the bounds.</returns>
    private static unsafe bool CoversBounds(nint candidate, PixelRect bounds, int ownProcessId)
    {
        if (!IsWindowVisible(candidate))
        {
            return false;
        }

        if (DwmGetWindowAttribute(candidate, DwmaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        if (GetWindowThreadProcessId(candidate, out var processId) != 0 && processId == (uint)ownProcessId)
        {
            return false;
        }

        var className = stackalloc char[ClassNameCapacity];
        var length = GetClassName(candidate, className, ClassNameCapacity);
        var name = new ReadOnlySpan<char>(className, length);
        if (name.SequenceEqual(MenuClassName) || name.SequenceEqual(TooltipClassName))
        {
            return false;
        }

        return GetWindowRect(candidate, out var other) && Overlaps(bounds, other.ToPixelRect());
    }

    /// <summary>
    /// Tells whether two rectangles share some area.
    /// </summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns><see langword="true"/> if they overlap; touching edges do not count.</returns>
    public static bool Overlaps(PixelRect first, PixelRect second)
    {
        return first.Left < second.Right && second.Left < first.Right
            && first.Top < second.Bottom && second.Top < first.Bottom;
    }

    /// <summary>
    /// Moves two windows to positions given in device pixels in a single batch, so the window manager applies both
    /// together and they are drawn in the same frame.
    /// </summary>
    /// <param name="first">The first window to move.</param>
    /// <param name="firstX">The first window's wanted left edge in device pixels.</param>
    /// <param name="firstY">The first window's wanted top edge in device pixels.</param>
    /// <param name="second">The second window to move.</param>
    /// <param name="secondX">The second window's wanted left edge in device pixels.</param>
    /// <param name="secondY">The second window's wanted top edge in device pixels.</param>
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
    /// <param name="windowCount">How many windows the batch is sized for.</param>
    /// <returns>The batch handle, or zero on failure.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint BeginDeferWindowPos(int windowCount);

    /// <summary>
    /// Adds one window move to a batch.
    /// </summary>
    /// <param name="batch">The batch handle.</param>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="insertAfter">Z-order neighbour; unused with <c>SWP_NOZORDER</c>.</param>
    /// <param name="x">New left edge in device pixels.</param>
    /// <param name="y">New top edge in device pixels.</param>
    /// <param name="width">New width; unused with <c>SWP_NOSIZE</c>.</param>
    /// <param name="height">New height; unused with <c>SWP_NOSIZE</c>.</param>
    /// <param name="flags"><c>SWP_*</c> flags.</param>
    /// <returns>The handle of the batch to use from now on, or zero on failure.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint DeferWindowPos(nint batch, nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    /// <summary>
    /// Applies a batch of window moves.
    /// </summary>
    /// <param name="batch">The batch handle.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndDeferWindowPos(nint batch);

    /// <summary>
    /// Changes the position of a window.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="insertAfter">Z-order neighbour; unused with <c>SWP_NOZORDER</c>.</param>
    /// <param name="x">New left edge in device pixels.</param>
    /// <param name="y">New top edge in device pixels.</param>
    /// <param name="width">New width; unused with <c>SWP_NOSIZE</c>.</param>
    /// <param name="height">New height; unused with <c>SWP_NOSIZE</c>.</param>
    /// <param name="flags"><c>SWP_*</c> flags.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    /// <summary>
    /// Gets a window next to another one in the z-order.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="command">Which neighbour to get (<c>GW_*</c>).</param>
    /// <returns>The neighbour's handle, or zero if there is none.</returns>
    [LibraryImport("user32.dll")]
    private static partial nint GetWindow(nint hwnd, uint command);

    /// <summary>
    /// A window rectangle as Windows fills it in.
    /// </summary>
    private struct NativeRect
    {
        /// <summary>
        /// Left edge.
        /// </summary>
        public int Left;

        /// <summary>
        /// Top edge.
        /// </summary>
        public int Top;

        /// <summary>
        /// Right edge.
        /// </summary>
        public int Right;

        /// <summary>
        /// Bottom edge.
        /// </summary>
        public int Bottom;

        /// <summary>
        /// Converts the rectangle to a <see cref="PixelRect"/>.
        /// </summary>
        /// <returns>The same edges as a pixel rectangle.</returns>
        public readonly PixelRect ToPixelRect() => new(Left, Top, Right, Bottom);
    }

    /// <summary>
    /// Retrieves the bounds of a window in screen device pixels.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="rect">Receives the bounds.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out NativeRect rect);

    /// <summary>
    /// Tells whether a window is visible.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <returns><see langword="true"/> if the window has the visible style.</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hwnd);

    /// <summary>
    /// Gets the id of the process that owns a window.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="processId">Receives the process id.</param>
    /// <returns>The id of the thread that created the window, or zero on failure.</returns>
    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    /// <summary>
    /// Reads the class name of a window.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="buffer">Receives the name.</param>
    /// <param name="capacity">Size of the buffer in characters.</param>
    /// <returns>The number of characters copied, or zero on failure.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    private static unsafe partial int GetClassName(nint hwnd, char* buffer, int capacity);

    /// <summary>
    /// Reads a DWM window attribute.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="attribute">Attribute identifier.</param>
    /// <param name="value">Receives the value.</param>
    /// <param name="size">Size of the value in bytes.</param>
    /// <returns>An HRESULT; non-zero on failure.</returns>
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
}
