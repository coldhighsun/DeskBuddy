using System.Globalization;

namespace DeskBuddy.Domain.Settings;

/// <summary>
/// A global keyboard shortcut: modifier keys plus one key, written as text like <c>Ctrl+Alt+H</c>. Only letters, digits
/// and function keys are supported, and at least one modifier is required so that a plain key is never taken over
/// system-wide.
/// </summary>
/// <param name="Modifiers">
/// The modifier keys.
/// </param>
/// <param name="VirtualKey">
/// The Win32 virtual-key code of the main key.
/// </param>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>
    /// Virtual-key code of the digit 0 (digits 0-9 are consecutive).
    /// </summary>
    private const int Digit0 = 0x30;

    /// <summary>
    /// Virtual-key code of the letter A (letters A-Z are consecutive).
    /// </summary>
    private const int LetterA = 0x41;

    /// <summary>
    /// Virtual-key code of F1 (F1-F24 are consecutive).
    /// </summary>
    private const int F1 = 0x70;

    /// <summary>
    /// Number of function keys supported.
    /// </summary>
    private const int FunctionKeyCount = 24;

    /// <summary>
    /// Gets a value indicating whether this gesture has at least one modifier and a supported main key.
    /// </summary>
    public bool IsValid => Modifiers != HotkeyModifiers.None && KeyName(VirtualKey) is not null;

    /// <summary>
    /// Parses text such as <c>Ctrl+Alt+H</c>. Names are case-insensitive; <c>Control</c>, <c>Win</c> and
    /// <c>Windows</c> are accepted as aliases.
    /// </summary>
    /// <param name="text">
    /// The text to parse.
    /// </param>
    /// <param name="gesture">
    /// The parsed gesture when successful.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the text is a valid gesture.
    /// </returns>
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        var key = 0;
        foreach (var rawPart in text.Split('+'))
        {
            var part = rawPart.Trim();
            var modifier = ParseModifier(part);
            if (modifier != HotkeyModifiers.None)
            {
                modifiers |= modifier;
                continue;
            }

            var parsedKey = ParseKey(part);
            if (key != 0 || parsedKey == 0)
            {
                return false;
            }

            key = parsedKey;
        }

        var candidate = new HotkeyGesture(modifiers, key);
        if (!candidate.IsValid)
        {
            return false;
        }

        gesture = candidate;
        return true;
    }

    /// <summary>
    /// Formats the gesture in its canonical form, e.g. <c>Ctrl+Alt+H</c>.
    /// </summary>
    /// <returns>
    /// The text form.
    /// </returns>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(VirtualKey) ?? $"0x{VirtualKey:X}");
        return string.Join('+', parts);
    }

    /// <summary>
    /// Maps a modifier name to its flag.
    /// </summary>
    /// <param name="name">
    /// The name to map.
    /// </param>
    /// <returns>
    /// The flag, or <see cref="HotkeyModifiers.None"/> if the name is not a modifier.
    /// </returns>
    private static HotkeyModifiers ParseModifier(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => HotkeyModifiers.Control,
            "ALT" => HotkeyModifiers.Alt,
            "SHIFT" => HotkeyModifiers.Shift,
            "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
            _ => HotkeyModifiers.None,
        };
    }

    /// <summary>
    /// Maps a key name (a letter, a digit or F1-F24) to its virtual-key code.
    /// </summary>
    /// <param name="name">
    /// The name to map.
    /// </param>
    /// <returns>
    /// The virtual-key code, or 0 if the name is not a supported key.
    /// </returns>
    private static int ParseKey(string name)
    {
        var upper = name.ToUpperInvariant();
        if (upper.Length == 1 && upper[0] is >= 'A' and <= 'Z')
        {
            return LetterA + (upper[0] - 'A');
        }

        if (upper.Length == 1 && upper[0] is >= '0' and <= '9')
        {
            return Digit0 + (upper[0] - '0');
        }

        if (upper.Length >= 2
            && upper[0] == 'F'
            && int.TryParse(upper.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number is >= 1 and <= FunctionKeyCount)
        {
            return F1 + number - 1;
        }

        return 0;
    }

    /// <summary>
    /// Maps a virtual-key code to its name.
    /// </summary>
    /// <param name="virtualKey">
    /// The virtual-key code.
    /// </param>
    /// <returns>
    /// The name, or <see langword="null"/> if the key is not supported.
    /// </returns>
    private static string? KeyName(int virtualKey)
    {
        return virtualKey switch
        {
            >= LetterA and <= LetterA + 25 => ((char)('A' + (virtualKey - LetterA))).ToString(),
            >= Digit0 and <= Digit0 + 9 => ((char)('0' + (virtualKey - Digit0))).ToString(),
            >= F1 and < F1 + FunctionKeyCount => $"F{virtualKey - F1 + 1}",
            _ => null,
        };
    }
}
