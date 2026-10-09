using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Application;

/// <summary>
/// Keeps the global boss key registered with whatever shortcut the settings name, and hides or shows the pet when it
/// is pressed. A shortcut that cannot be registered does not stop the application: the error is kept in
/// <see cref="RegistrationError"/> for the settings page and the previous shortcut, if any, keeps working.
/// </summary>
public sealed class BossKeyController : IDisposable
{
    /// <summary>
    /// The settings that name the shortcut.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// Registers the shortcut with the system.
    /// </summary>
    private readonly IGlobalHotkeyService _hotkeys;

    /// <summary>
    /// The brain whose visibility the shortcut toggles.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossKeyController"/> class and registers the current shortcut.
    /// </summary>
    /// <param name="settings">
    /// The settings that name the shortcut.
    /// </param>
    /// <param name="hotkeys">
    /// Registers the shortcut with the system.
    /// </param>
    /// <param name="brain">
    /// The brain whose visibility the shortcut toggles.
    /// </param>
    public BossKeyController(SettingsService settings, IGlobalHotkeyService hotkeys, PetBrain brain)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(hotkeys);
        ArgumentNullException.ThrowIfNull(brain);

        _settings = settings;
        _hotkeys = hotkeys;
        _brain = brain;

        _hotkeys.Pressed += OnPressed;
        _settings.Changed += OnSettingsChanged;
        Apply(_settings.Current.BossKey);
    }

    /// <summary>
    /// Raised when <see cref="Registered"/> or <see cref="RegistrationError"/> changed.
    /// </summary>
    public event EventHandler? StatusChanged;

    /// <summary>
    /// Gets the shortcut that is registered with the system, or <see langword="null"/> if none could be.
    /// </summary>
    public HotkeyGesture? Registered { get; private set; }

    /// <summary>
    /// Gets why the shortcut named in the settings could not be registered, or <see langword="null"/> if it could.
    /// </summary>
    public string? RegistrationError { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        _hotkeys.Pressed -= OnPressed;
        _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>
    /// Hides or shows the pet.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnPressed(object? sender, EventArgs e)
    {
        _brain.ToggleManuallyHidden();
    }

    /// <summary>
    /// Re-registers the shortcut when the settings name a different one.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        Apply(settings.BossKey);
    }

    /// <summary>
    /// Registers a shortcut unless it is the one already registered, and records the outcome.
    /// </summary>
    /// <param name="gesture">
    /// The shortcut the settings name.
    /// </param>
    private void Apply(HotkeyGesture gesture)
    {
        if (Registered == gesture)
        {
            // Already in effect; a failed attempt at some other shortcut no longer matters.
            if (RegistrationError is not null)
            {
                RegistrationError = null;
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (_hotkeys.TryRegister(gesture, out var error))
        {
            Registered = gesture;
            RegistrationError = null;
        }
        else
        {
            RegistrationError = error ?? "The shortcut could not be registered.";
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
