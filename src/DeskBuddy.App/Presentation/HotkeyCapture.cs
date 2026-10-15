using System.Windows.Input;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// What a key press means while the user is entering a new shortcut.
/// </summary>
public enum HotkeyCaptureResult
{
    /// <summary>
    /// Only modifier keys are down so far; the entry continues.
    /// </summary>
    Waiting,

    /// <summary>
    /// The key press cannot be a shortcut (no modifier, or a key the shortcut cannot use).
    /// </summary>
    Rejected,

    /// <summary>
    /// The key press is a valid shortcut.
    /// </summary>
    Accepted,
}

/// <summary>
/// Turns the keys of a WPF key press into a <see cref="HotkeyGesture"/> while the user records a shortcut.
/// </summary>
public static class HotkeyCapture
{
    /// <summary>
    /// Converts a key press.
    /// </summary>
    /// <param name="key">
    /// The key of the event. When Alt is held WPF reports <see cref="Key.System"/> here.
    /// </param>
    /// <param name="systemKey">
    /// The real key when <paramref name="key"/> is <see cref="Key.System"/>.
    /// </param>
    /// <param name="modifiers">
    /// The modifier keys held down.
    /// </param>
    /// <param name="gesture">
    /// The shortcut when the result is <see cref="HotkeyCaptureResult.Accepted"/>.
    /// </param>
    /// <returns>
    /// Whether the press is a shortcut, still only modifiers, or cannot be used.
    /// </returns>
    public static HotkeyCaptureResult TryCreate(Key key, Key systemKey, ModifierKeys modifiers, out HotkeyGesture gesture)
    {
        gesture = default;

        var actual = key == Key.System ? systemKey : key;
        if (IsModifierKey(actual))
        {
            return HotkeyCaptureResult.Waiting;
        }

        var mapped = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            mapped |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            mapped |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            mapped |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            mapped |= HotkeyModifiers.Windows;
        }

        var candidate = new HotkeyGesture(mapped, KeyInterop.VirtualKeyFromKey(actual));
        if (!candidate.IsValid)
        {
            return HotkeyCaptureResult.Rejected;
        }

        gesture = candidate;
        return HotkeyCaptureResult.Accepted;
    }

    /// <summary>
    /// Determines whether a key is a modifier key (or no key at all).
    /// </summary>
    /// <param name="key">
    /// The key to check.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for Ctrl, Alt, Shift and Windows keys.
    /// </returns>
    private static bool IsModifierKey(Key key)
    {
        return key is Key.None
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
    }
}
