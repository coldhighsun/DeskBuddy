using System.Windows;
using System.Windows.Interop;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Win32 helpers that tweak extended window styles for the pet window.
/// </summary>
internal static partial class WindowStyleHelper
{
    /// <summary>
    /// Index of the extended window style for <c>GetWindowLongPtr</c>/<c>SetWindowLongPtr</c>.
    /// </summary>
    private const int GwlExStyle = -20;

    /// <summary>
    /// Tool window style: hides the window from Alt-Tab and the taskbar.
    /// </summary>
    private const nint WsExToolWindow = 0x00000080;

    /// <summary>
    /// No-activate style: clicking the window does not steal focus from the user's current app.
    /// </summary>
    private const nint WsExNoActivate = 0x08000000;

    /// <summary>
    /// Hides a window from Alt-Tab and prevents it from taking focus when clicked.
    /// </summary>
    /// <param name="window">
    /// The window, which must already have a handle (call after <see cref="Window.SourceInitialized"/>).
    /// </param>
    public static void MakeToolWindowNoActivate(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle);
        SetWindowLongPtr(handle, GwlExStyle, style | WsExToolWindow | WsExNoActivate);
    }

    /// <summary>
    /// <c>WM_NCHITTEST</c> window message.
    /// </summary>
    public const int WmNcHitTest = 0x0084;

    /// <summary>
    /// <c>WM_SETTINGCHANGE</c>: a system setting changed; <c>wParam</c> says which.
    /// </summary>
    public const int WmSettingChange = 0x001A;

    /// <summary>
    /// <c>WM_DISPLAYCHANGE</c>: the resolution or the set of monitors changed.
    /// </summary>
    public const int WmDisplayChange = 0x007E;

    /// <summary>
    /// <c>WM_DPICHANGED</c>: the window moved to a monitor of different scaling, or the scaling changed.
    /// </summary>
    public const int WmDpiChanged = 0x02E0;

    /// <summary>
    /// <c>SPI_SETWORKAREA</c>: the <c>wParam</c> of <see cref="WmSettingChange"/> when the work area changed, for
    /// example because the taskbar moved or started hiding itself.
    /// </summary>
    public const int SpiSetWorkArea = 0x002F;

    /// <summary>
    /// Hit-test result meaning the point is in the client area.
    /// </summary>
    public const int HtClient = 1;

    /// <summary>
    /// Hit-test result meaning the point is transparent and the mouse event goes to the window underneath.
    /// </summary>
    public const int HtTransparent = -1;

    /// <summary>
    /// A window procedure hook that lets every mouse click pass through the window.
    /// </summary>
    /// <param name="hwnd">The window handle.</param>
    /// <param name="msg">The message.</param>
    /// <param name="wParam">The first message parameter.</param>
    /// <param name="lParam">The second message parameter.</param>
    /// <param name="handled">Set when the message was dealt with.</param>
    /// <returns><c>HTTRANSPARENT</c> for hit tests, otherwise zero.</returns>
    public static nint ClickThroughHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmNcHitTest)
        {
            handled = true;
            return HtTransparent;
        }

        return 0;
    }

    /// <summary>
    /// DWM attribute selecting the Windows 11 corner style.
    /// </summary>
    private const int DwmwaWindowCornerPreference = 33;

    /// <summary>
    /// DWM attribute selecting the Windows 11 window border color.
    /// </summary>
    private const int DwmwaBorderColor = 34;

    /// <summary>
    /// Corner preference value that disables rounded corners.
    /// </summary>
    private const int DwmwcpDoNotRound = 1;

    /// <summary>
    /// Border color value that removes the border.
    /// </summary>
    private const uint DwmwaColorNone = 0xFFFFFFFE;

    /// <summary>
    /// Removes the Windows 11 rounded corners and 1px border that DWM would otherwise draw around the glass window.
    /// Silently does nothing on Windows versions that do not support these attributes.
    /// </summary>
    /// <param name="window">The window, which must already have a handle.</param>
    public static void RemoveDwmDecorations(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var corner = DwmwcpDoNotRound;
        var border = DwmwaColorNone;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, sizeof(uint));
    }

    /// <summary>
    /// Corner preference value that asks for the standard rounded corners.
    /// </summary>
    internal const int DwmwcpRound = 2;

    /// <summary>
    /// Corner preference value that gives the corners back to the system.
    /// </summary>
    internal const int DwmwcpDefault = 0;

    /// <summary>
    /// Sets the Windows 11 corner preference of a window. Silently does nothing on Windows versions that do not support
    /// it.
    /// </summary>
    /// <param name="handle">Window handle.</param>
    /// <param name="preference">One of the <c>DWMWCP_*</c> values.</param>
    internal static void SetCornerPreference(nint handle, int preference)
    {
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    /// <summary>
    /// Converts the screen coordinates packed in a <c>WM_NCHITTEST</c> <c>lParam</c> into client coordinates.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="lParam">The message <c>lParam</c>: signed 16-bit x in the low word, y in the high word.</param>
    /// <returns>The point in client device pixels.</returns>
    public static (int X, int Y) ScreenLParamToClient(nint hwnd, nint lParam)
    {
        var point = new NativePoint
        {
            X = (short)(lParam.ToInt64() & 0xFFFF),
            Y = (short)((lParam.ToInt64() >> 16) & 0xFFFF),
        };
        _ = ScreenToClient(hwnd, ref point);
        return (point.X, point.Y);
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
    /// Sets a DWM window attribute.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="attribute">Attribute identifier.</param>
    /// <param name="value">Attribute value.</param>
    /// <param name="size">Size of the value in bytes.</param>
    /// <returns>An HRESULT; non-zero on failure.</returns>
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Sets a DWM window attribute with an unsigned value.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="attribute">Attribute identifier.</param>
    /// <param name="value">Attribute value.</param>
    /// <param name="size">Size of the value in bytes.</param>
    /// <returns>An HRESULT; non-zero on failure.</returns>
    [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref uint value, int size);

    /// <summary>
    /// Converts screen coordinates to client coordinates.
    /// </summary>
    /// <param name="hwnd">Window handle.</param>
    /// <param name="point">The point to convert in place.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ScreenToClient(nint hwnd, ref NativePoint point);

    /// <summary>
    /// Reads a window long value.
    /// </summary>
    /// <param name="hWnd">Window handle.</param>
    /// <param name="nIndex">Value index.</param>
    /// <returns>The requested value.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    /// <summary>
    /// Writes a window long value.
    /// </summary>
    /// <param name="hWnd">Window handle.</param>
    /// <param name="nIndex">Value index.</param>
    /// <param name="dwNewLong">New value.</param>
    /// <returns>The previous value.</returns>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    /// <summary>
    /// Tells whether a window message means that the screens changed in a way that can move the taskbar line or the
    /// edges of the monitors.
    /// </summary>
    /// <param name="msg">The window message.</param>
    /// <param name="wParam">Its <c>wParam</c>.</param>
    /// <returns><see langword="true"/> for a change of monitors, resolution, scaling or work area.</returns>
    public static bool IsScreenLayoutChange(int msg, nint wParam)
    {
        return msg is WmDisplayChange or WmDpiChanged
            || (msg == WmSettingChange && wParam == SpiSetWorkArea);
    }
}
