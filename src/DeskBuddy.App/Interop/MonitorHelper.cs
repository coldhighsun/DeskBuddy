using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DeskBuddy.App.Presentation;
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
    /// Flag for <c>MonitorFromPoint</c>: if the point is on no monitor, return nothing.
    /// </summary>
    private const uint MonitorDefaultToNull = 0;

    /// <summary>
    /// The kind of resolution <c>GetDpiForMonitor</c> reports that is the one the user has chosen in the settings.
    /// </summary>
    private const int EffectiveDpi = 0;

    /// <summary>
    /// The resolution of a monitor at 100% scaling.
    /// </summary>
    private const double DefaultDpi = 96;

    /// <summary>
    /// Tells whether another monitor lies right beside the one the window is on, at the height of the window.
    /// </summary>
    /// <param name="window">
    /// The window, which must already have a handle.
    /// </param>
    /// <param name="left">
    /// <see langword="true"/> to look beside the left edge, <see langword="false"/> for the right edge.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if there is a monitor there, so that the edge is only a seam between two monitors.
    /// </returns>
    public static bool HasMonitorBeside(Window window, bool left)
    {
        if (!TryGetPlacement(window, out var info, out var bounds))
        {
            return false;
        }

        var beside = new NativePoint
        {
            X = left ? info.Monitor.Left - 1 : info.Monitor.Right,
            Y = (bounds.Top + bounds.Bottom) / 2,
        };
        return MonitorFromPoint(beside, MonitorDefaultToNull) != 0;
    }

    /// <summary>
    /// Tells whether another monitor lies right above the one the window is on, at the horizontal position of the window.
    /// </summary>
    /// <param name="window">
    /// The window, which must already have a handle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if there is a monitor there, so that the top edge is only a seam between two monitors.
    /// </returns>
    public static bool HasMonitorAbove(Window window)
    {
        if (!TryGetPlacement(window, out var info, out var bounds))
        {
            return false;
        }

        var above = new NativePoint
        {
            X = (bounds.Left + bounds.Right) / 2,
            Y = info.Monitor.Top - 1,
        };
        return MonitorFromPoint(above, MonitorDefaultToNull) != 0;
    }

    /// <summary>
    /// Gets the work area (screen minus taskbar) and the scaling of every monitor.
    /// </summary>
    /// <returns>
    /// One entry per monitor; empty if the monitors cannot be listed completely.
    /// </returns>
    public static unsafe IReadOnlyList<MonitorArea> GetAllMonitors()
    {
        var areas = new List<MonitorArea>();
        var handle = GCHandle.Alloc(areas);
        try
        {
            // A list that is not complete is worse than none: the caller then falls back to the monitor it is on.
            if (!EnumDisplayMonitors(0, 0, &CollectWorkArea, GCHandle.ToIntPtr(handle)))
            {
                areas.Clear();
            }
        }
        finally
        {
            handle.Free();
        }

        return areas;
    }

    /// <summary>
    /// Gets how far a window may walk: across the row of monitors that lie side by side, starting from the one it is on.
    /// </summary>
    /// <param name="window">
    /// The window, which must already have a handle.
    /// </param>
    /// <returns>
    /// The range in device pixels. Falls back to the work area of the monitor the window is on, without seams, if the
    /// monitors cannot be listed.
    /// </returns>
    public static WalkRange GetWalkRangePixels(Window window)
    {
        var bounds = GetWindowBoundsPixels(window);

        // The centre in whole pixels, as the pet window works it out when it checks that it is still on this monitor.
        var range = WalkRange.Compute(
            GetAllMonitors(),
            bounds.Left + (bounds.Width / 2),
            bounds.Top + (bounds.Height / 2),
            bounds.Height);
        if (range is { } found)
        {
            return found;
        }

        var work = GetWorkAreaPixels(window);
        return new WalkRange(work.Left, work.Right, work.Top, work.Bottom, work, [], VisualTreeHelper.GetDpi(window).DpiScaleX, [work]);
    }

    /// <summary>
    /// Looks up the monitor a window is on and the window's bounds.
    /// </summary>
    /// <param name="window">
    /// The window, which must already have a handle.
    /// </param>
    /// <param name="info">
    /// Receives the information about the monitor.
    /// </param>
    /// <param name="bounds">
    /// Receives the bounds of the window in device pixels.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if both could be read.
    /// </returns>
    private static bool TryGetPlacement(Window window, out MonitorInfo info, out NativeRect bounds)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        bounds = default;
        return monitor != 0 && GetMonitorInfo(monitor, ref info) && GetWindowRect(handle, out bounds);
    }

    /// <summary>
    /// Called by <c>EnumDisplayMonitors</c> for every monitor: adds its work area and scaling to the list the caller passed
    /// in.
    /// </summary>
    /// <param name="monitor">
    /// The monitor handle.
    /// </param>
    /// <param name="deviceContext">
    /// Unused.
    /// </param>
    /// <param name="bounds">
    /// Unused.
    /// </param>
    /// <param name="data">
    /// The handle of the <see cref="List{T}"/> of <see cref="MonitorArea"/> to add to.
    /// </param>
    /// <returns>
    /// Nonzero to carry on with the next monitor.
    /// </returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe int CollectWorkArea(nint monitor, nint deviceContext, NativeRect* bounds, nint data)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(monitor, ref info) && GCHandle.FromIntPtr(data).Target is List<MonitorArea> areas)
        {
            // Without the scaling of the monitor (the call fails) it is taken as 100%.
            var scale = GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0 ? dpiX / DefaultDpi : 1;
            areas.Add(new MonitorArea(
                new PixelRect(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom),
                scale,
                new PixelRect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom)));
        }

        return 1;
    }

    /// <summary>
    /// Gets the mouse cursor position on the screen in device pixels of the virtual screen, which mean the same on every
    /// monitor whatever its scaling.
    /// </summary>
    /// <returns>
    /// The cursor position in device pixels.
    /// </returns>
    public static (int X, int Y) GetCursorPositionPixels()
    {
        GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    /// <summary>
    /// Gets the bounds of a window in device pixels of the virtual screen.
    /// </summary>
    /// <param name="window">
    /// The window, which must already have a handle.
    /// </param>
    /// <returns>
    /// The bounds, or an empty rectangle if they cannot be read.
    /// </returns>
    public static PixelRect GetWindowBoundsPixels(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return GetWindowRect(handle, out var rect) ? new PixelRect(rect.Left, rect.Top, rect.Right, rect.Bottom) : default;
    }

    /// <summary>
    /// Gets the work area (screen minus taskbar) of the monitor a window is on, in device pixels of the virtual screen.
    /// </summary>
    /// <param name="window">
    /// The window, which must already have a handle.
    /// </param>
    /// <returns>
    /// The work area, or the primary monitor's if the monitor cannot be queried.
    /// </returns>
    public static PixelRect GetWorkAreaPixels(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
        {
            var primary = SystemParameters.WorkArea;
            var scale = VisualTreeHelper.GetDpi(window);
            return new PixelRect(
                (int)(primary.Left * scale.DpiScaleX),
                (int)(primary.Top * scale.DpiScaleY),
                (int)(primary.Right * scale.DpiScaleX),
                (int)(primary.Bottom * scale.DpiScaleY));
        }

        return new PixelRect(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
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
    /// Gets the monitor that contains a point.
    /// </summary>
    /// <param name="point">
    /// The point in screen device pixels.
    /// </param>
    /// <param name="flags">
    /// Fallback behavior when the point is on no monitor.
    /// </param>
    /// <returns>
    /// The monitor handle, or zero if there is none.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(NativePoint point, uint flags);

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
    /// Retrieves the bounds of a window in screen device pixels.
    /// </summary>
    /// <param name="hwnd">
    /// Window handle.
    /// </param>
    /// <param name="rect">
    /// Receives the bounds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out NativeRect rect);

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

    /// <summary>
    /// Gets the resolution of a monitor.
    /// </summary>
    /// <param name="monitor">
    /// The monitor handle.
    /// </param>
    /// <param name="type">
    /// The kind of resolution to get.
    /// </param>
    /// <param name="dpiX">
    /// Receives the horizontal resolution in dots per inch.
    /// </param>
    /// <param name="dpiY">
    /// Receives the vertical resolution in dots per inch.
    /// </param>
    /// <returns>
    /// Zero (<c>S_OK</c>) on success.
    /// </returns>
    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    /// <summary>
    /// Calls a function for every monitor.
    /// </summary>
    /// <param name="deviceContext">
    /// Zero, for all monitors of the virtual screen.
    /// </param>
    /// <param name="clip">
    /// Zero, for no clipping.
    /// </param>
    /// <param name="callback">
    /// The function to call for every monitor.
    /// </param>
    /// <param name="data">
    /// A value handed to the function on every call.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool EnumDisplayMonitors(
        nint deviceContext,
        nint clip,
        delegate* unmanaged[Stdcall]<nint, nint, NativeRect*, nint, int> callback,
        nint data);
}
