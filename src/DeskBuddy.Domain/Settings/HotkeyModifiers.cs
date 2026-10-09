namespace DeskBuddy.Domain.Settings;

/// <summary>
/// Modifier keys of a <see cref="HotkeyGesture"/>. The values match the Win32 <c>MOD_*</c> constants.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>
    /// No modifier.
    /// </summary>
    None = 0,

    /// <summary>
    /// The Alt key.
    /// </summary>
    Alt = 0x1,

    /// <summary>
    /// The Ctrl key.
    /// </summary>
    Control = 0x2,

    /// <summary>
    /// The Shift key.
    /// </summary>
    Shift = 0x4,

    /// <summary>
    /// The Windows key.
    /// </summary>
    Windows = 0x8,
}
