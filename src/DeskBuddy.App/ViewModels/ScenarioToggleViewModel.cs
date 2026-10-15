using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One switch in the settings: whether the pet acts out a scenario.
/// </summary>
/// <param name="state">
/// The scenario, identified by the state the pet shows for it.
/// </param>
/// <param name="label">
/// The text shown next to the switch.
/// </param>
/// <param name="onChanged">
/// Called when the user flips the switch.
/// </param>
public sealed partial class ScenarioToggleViewModel(PetState state, string label, Action<PetState, bool> onChanged)
    : ObservableObject
{
    /// <summary>
    /// Whether the value is being set from the settings, which is not the user flipping the switch.
    /// </summary>
    private bool _silent;

    /// <summary>
    /// Gets the scenario, identified by the state the pet shows for it.
    /// </summary>
    public PetState State { get; } = state;

    /// <summary>
    /// Gets the text shown next to the switch.
    /// </summary>
    public string Label { get; } = label;

    /// <summary>
    /// Gets or sets a value indicating whether the pet acts out the scenario.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>
    /// Shows a value from the settings without reporting it as the user's change.
    /// </summary>
    /// <param name="isEnabled">
    /// Whether the scenario is on.
    /// </param>
    public void Show(bool isEnabled)
    {
        _silent = true;
        try
        {
            IsEnabled = isEnabled;
        }
        finally
        {
            _silent = false;
        }
    }

    /// <summary>
    /// Reports the user's change.
    /// </summary>
    /// <param name="value">
    /// The new value.
    /// </param>
    partial void OnIsEnabledChanged(bool value)
    {
        if (!_silent)
        {
            onChanged(State, value);
        }
    }
}
