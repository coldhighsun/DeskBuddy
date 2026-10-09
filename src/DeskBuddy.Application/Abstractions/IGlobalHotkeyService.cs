using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Registers one system-wide keyboard shortcut.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>
    /// Raised, on the UI thread, each time the registered shortcut is pressed.
    /// </summary>
    event EventHandler? Pressed;

    /// <summary>
    /// Registers a shortcut in place of the current one. If the new shortcut cannot be registered (typically because
    /// another program already uses it) the current registration stays in effect.
    /// </summary>
    /// <param name="gesture">
    /// The shortcut to register.
    /// </param>
    /// <param name="error">
    /// Why registration failed; <see langword="null"/> on success.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the shortcut is now registered.
    /// </returns>
    bool TryRegister(HotkeyGesture gesture, out string? error);
}
