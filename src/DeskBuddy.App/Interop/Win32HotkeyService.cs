using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Registers a system-wide shortcut with <c>RegisterHotKey</c>. Key presses arrive as <c>WM_HOTKEY</c> messages at a
/// hidden message-only window, so they are delivered on the UI thread. This class must be created on that thread.
/// </summary>
internal sealed partial class Win32HotkeyService : IGlobalHotkeyService
{
    /// <summary>
    /// <c>WM_HOTKEY</c> window message.
    /// </summary>
    private const int WmHotKey = 0x0312;

    /// <summary>
    /// <c>MOD_NOREPEAT</c>: holding the keys down raises the shortcut once instead of repeatedly.
    /// </summary>
    private const uint ModNoRepeat = 0x4000;

    /// <summary>
    /// Parent handle that makes a window a message-only window (<c>HWND_MESSAGE</c>).
    /// </summary>
    private const int HwndMessage = -3;

    /// <summary>
    /// The identifier a hotkey is registered under. Two are used in turn so that a new shortcut can be registered
    /// before the old one is released.
    /// </summary>
    private static readonly int[] HotkeyIds = [1, 2];

    /// <summary>
    /// The hidden window that receives the key presses.
    /// </summary>
    private readonly HwndSource _source;

    /// <summary>
    /// Index in <see cref="HotkeyIds"/> of the identifier currently registered, or -1 if none.
    /// </summary>
    private int _activeIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="Win32HotkeyService"/> class and creates its hidden window.
    /// </summary>
    public Win32HotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("DeskBuddyHotkeys")
        {
            ParentWindow = HwndMessage,
            Width = 0,
            Height = 0,
        });
        _source.AddHook(WndProc);
    }

    /// <inheritdoc />
    public event EventHandler? Pressed;

    /// <inheritdoc />
    public bool TryRegister(HotkeyGesture gesture, out string? error)
    {
        var index = _activeIndex == 0 ? 1 : 0;
        if (!RegisterHotKey(_source.Handle, HotkeyIds[index], (uint)gesture.Modifiers | ModNoRepeat, (uint)gesture.VirtualKey))
        {
            error = new Win32Exception(Marshal.GetLastPInvokeError()).Message;
            return false;
        }

        if (_activeIndex >= 0)
        {
            UnregisterHotKey(_source.Handle, HotkeyIds[_activeIndex]);
        }

        _activeIndex = index;
        error = null;
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_activeIndex >= 0)
        {
            UnregisterHotKey(_source.Handle, HotkeyIds[_activeIndex]);
            _activeIndex = -1;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    /// <summary>
    /// Raises <see cref="Pressed"/> for <c>WM_HOTKEY</c> messages.
    /// </summary>
    /// <param name="hwnd">
    /// Window handle.
    /// </param>
    /// <param name="msg">
    /// Window message.
    /// </param>
    /// <param name="wParam">
    /// Message <c>wParam</c>; the hotkey identifier for <c>WM_HOTKEY</c>.
    /// </param>
    /// <param name="lParam">
    /// Message <c>lParam</c>.
    /// </param>
    /// <param name="handled">
    /// Set to <see langword="true"/> when the message was processed.
    /// </param>
    /// <returns>
    /// Always zero.
    /// </returns>
    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotKey && _activeIndex >= 0 && wParam == HotkeyIds[_activeIndex])
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return 0;
    }

    /// <summary>
    /// Registers a system-wide hotkey for a window.
    /// </summary>
    /// <param name="window">
    /// The window that receives <c>WM_HOTKEY</c>.
    /// </param>
    /// <param name="id">
    /// Identifier of the hotkey.
    /// </param>
    /// <param name="modifiers">
    /// Modifier keys (<c>MOD_*</c>).
    /// </param>
    /// <param name="virtualKey">
    /// Virtual-key code.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; false if another program already uses the combination.
    /// </returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    /// <summary>
    /// Releases a hotkey registered with <see cref="RegisterHotKey"/>.
    /// </summary>
    /// <param name="window">
    /// The window the hotkey was registered for.
    /// </param>
    /// <param name="id">
    /// Identifier of the hotkey.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint window, int id);
}
