namespace DeskBuddy.App.ViewModels;

/// <summary>
/// Groups the view models of the panel window's tabs.
/// </summary>
/// <param name="Settings">
/// The settings tab.
/// </param>
public sealed record PanelViewModel(
    SettingsTabViewModel Settings);
