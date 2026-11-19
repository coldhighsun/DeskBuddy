using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One choice in the language list of the settings.
/// </summary>
/// <param name="value">
/// The language.
/// </param>
/// <param name="label">
/// The name shown to the user.
/// </param>
public sealed partial class LanguageOption(AppLanguage value, string label) : ObservableObject
{
    /// <summary>
    /// Gets the language.
    /// </summary>
    public AppLanguage Value { get; } = value;

    /// <summary>
    /// Gets or sets the name shown to the user, which is written again when the interface language changes.
    /// </summary>
    [ObservableProperty]
    private string _label = label;
}
