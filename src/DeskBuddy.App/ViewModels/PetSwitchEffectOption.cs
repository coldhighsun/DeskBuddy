using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One choice in the list of switch animations of the settings.
/// </summary>
/// <param name="value">The effect.</param>
/// <param name="label">The name shown to the user.</param>
public sealed partial class PetSwitchEffectOption(PetSwitchEffect value, string label) : ObservableObject
{
    /// <summary>
    /// Gets the effect.
    /// </summary>
    public PetSwitchEffect Value { get; } = value;

    /// <summary>
    /// Gets or sets the name shown to the user, which is written again when the interface language changes.
    /// </summary>
    [ObservableProperty]
    private string _label = label;
}
