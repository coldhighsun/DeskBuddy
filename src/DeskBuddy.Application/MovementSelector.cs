using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Application;

/// <summary>
/// Picks the movement strategy that matches the disturbance mode in the settings and announces when the mode changes.
/// </summary>
public sealed class MovementSelector
{
    /// <summary>
    /// The strategy for <see cref="DisturbanceMode.Docked"/>.
    /// </summary>
    private readonly IMovementStrategy _docked;

    /// <summary>
    /// The strategy for <see cref="DisturbanceMode.Roam"/>.
    /// </summary>
    private readonly IMovementStrategy _roam;

    /// <summary>
    /// The mode in effect.
    /// </summary>
    private DisturbanceMode _mode;

    /// <summary>
    /// Initializes a new instance of the <see cref="MovementSelector"/> class.
    /// </summary>
    /// <param name="settings">
    /// The settings that name the mode.
    /// </param>
    /// <param name="docked">
    /// The strategy for the docked mode.
    /// </param>
    /// <param name="roam">
    /// The strategy for the roaming mode.
    /// </param>
    public MovementSelector(SettingsService settings, DockedMovement docked, RoamMovement roam)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(docked);
        ArgumentNullException.ThrowIfNull(roam);

        _docked = docked;
        _roam = roam;
        _mode = settings.Current.Mode;
        settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    /// Raised after <see cref="Current"/> changed because the mode in the settings changed.
    /// </summary>
    public event EventHandler? ModeChanged;

    /// <summary>
    /// Gets the strategy for the mode in effect.
    /// </summary>
    public IMovementStrategy Current => _mode == DisturbanceMode.Roam ? _roam : _docked;

    /// <summary>
    /// Switches strategy when the mode in the settings changed.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        if (settings.Mode == _mode)
        {
            return;
        }

        _mode = settings.Mode;
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }
}
