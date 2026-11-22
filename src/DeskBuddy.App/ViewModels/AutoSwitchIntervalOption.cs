using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One choice in the list of intervals between two automatic changes of the pet.
/// </summary>
/// <param name="value">The interval.</param>
/// <param name="label">The name shown to the user.</param>
public sealed partial class AutoSwitchIntervalOption(TimeSpan value, string label) : ObservableObject
{
    /// <summary>
    /// Gets the interval.
    /// </summary>
    public TimeSpan Value { get; } = value;

    /// <summary>
    /// Gets or sets the name shown to the user, which is written again when the interface language changes.
    /// </summary>
    [ObservableProperty]
    private string _label = label;
}
