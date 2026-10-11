using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One choice in the language list of the settings.
/// </summary>
/// <param name="Value">
/// The language.
/// </param>
/// <param name="Label">
/// The name shown to the user.
/// </param>
public sealed record LanguageOption(AppLanguage Value, string Label);
