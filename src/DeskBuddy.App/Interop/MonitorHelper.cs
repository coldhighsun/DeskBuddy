using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Win32 helpers for multi-monitor aware placement and drag tracking.
/// </summary>
internal static partial class MonitorHelper
{
    /// <summary>
    /// Flag for <c>MonitorFromWindow</c>: if the window is on no monitor, use the nearest one.
    /// </summary>
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>
    /// Gets the work area (screen minus taskbar) of the monitor the window is on, in device-independent pixels.
    /// </summary>
    /// <param name="window">
    /// The window, which must already have a handle.
    /// </param>
    /// <returns>
    /// The work area in DIPs. Falls back to the primary work area if the monitor cannot be queried.
    /// </returns>
    public static Rect GetWorkAreaDip(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
        {
            return SystemParameters.WorkArea;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        return new Rect(
            info.Work.Left / dpi.DpiScaleX,
            info.Work.Top / dpi.DpiScaleY,
            (info.Work.Right - info.Work.Left) / dpi.DpiScaleX,
            (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
    }

    /// <summary>
    /// Gets the mouse cursor position on the screen, in device-independent pixels of the given window's DPI.
    /// Reads the cursor directly instead of via WPF so values stay stable while the window itself is moving.
    /// </summary>
    /// <param name="window">
    /// The window whose DPI is used for the conversion.
    /// </param>
    /// <returns>
    /// The cursor position in DIPs.
    /// </returns>
    public static Point GetCursorPositionDip(Window window)
    {
        GetCursorPos(out var point);
        var dpi = VisualTreeHelper.GetDpi(window);
        return new Point(point.X / dpi.DpiScaleX, point.Y / dpi.DpiScaleY);
    }

    /// <summary>
    /// Rectangle in device pixels, matching the Win32 <c>RECT</c> layout.
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
    }

    /// <summary>
    /// Point in device pixels, matching the Win32 <c>POINT</c> layout.
    /// </summary>
    private struct NativePoint
    {
        /// <summary>
        /// Horizontal coordinate.
        /// </summary>
        public int X;

        /// <summary>
        /// Vertical coordinate.
        /// </summary>
        public int Y;
    }

    /// <summary>
    /// Matches the Win32 <c>MONITORINFO</c> structure.
    /// </summary>
    private struct MonitorInfo
    {
        /// <summary>
        /// Size of the structure in bytes.
        /// </summary>
        public int Size;

        /// <summary>
        /// Full monitor bounds.
        /// </summary>
        public NativeRect Monitor;

        /// <summary>
        /// Monitor bounds excluding the taskbar and docked toolbars.
        /// </summary>
        public NativeRect Work;

        /// <summary>
        /// Monitor flags.
        /// </summary>
        public uint Flags;
    }

    /// <summary>
    /// Gets the monitor that contains or is nearest to a window.
    /// </summary>
    /// <param name="hwnd">
    /// Window handle.
    /// </param>
    /// <param name="flags">
    /// Fallback behavior when the window is on no monitor.
    /// </param>
    /// <returns>
    /// The monitor handle.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hwnd, uint flags);

    /// <summary>
    /// Retrieves information about a monitor.
    /// </summary>
    /// <param name="monitor">
    /// Monitor handle.
    /// </param>
    /// <param name="info">
    /// Receives the information; <see cref="MonitorInfo.Size"/> must be set by the caller.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    /// <summary>
    /// Retrieves the cursor position in screen device pixels.
    /// </summary>
    /// <param name="point">
    /// Receives the position.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);
}
