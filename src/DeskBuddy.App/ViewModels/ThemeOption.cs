using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One choice in the theme list of the settings.
/// </summary>
/// <param name="Value">
/// The theme.
/// </param>
/// <param name="Label">
/// The name shown to the user.
/// </param>
public sealed record ThemeOption(AppTheme Value, string Label);
