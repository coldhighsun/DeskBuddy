using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One choice in the pet size list of the settings.
/// </summary>
/// <param name="Value">
/// The size.
/// </param>
/// <param name="Label">
/// The name shown to the user.
/// </param>
public sealed record PetSizeOption(PetSize Value, string Label);
