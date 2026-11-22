using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.ViewModels;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App;

/// <summary>
/// The part of the application that owns the panel window and the folders it opens.
/// </summary>
public partial class App
{
    /// <summary>
    /// Shows the panel window, creating it the first time.
    /// </summary>
    /// <param name="tab">The tab to show; <see langword="null"/> keeps the tab that was showing.</param>
    private void OpenPanel(PanelTab? tab)
    {
        if (_panel is null && PreparePanelViewModel() is { } viewModel)
        {
            _panel = new PanelWindow(viewModel, _updates, _updateLogger);
        }

        _panel?.ShowPanel(tab);
    }

    /// <summary>
    /// Creates the view model of the panel the first time, and starts looking at the pets in it, so that they are ready
    /// when the panel is opened. Called once the application is idle after start-up, and again when the panel opens.
    /// </summary>
    /// <returns>The view model, or <see langword="null"/> if the application is not up yet.</returns>
    private PanelViewModel? PreparePanelViewModel()
    {
        if (_panelViewModel is null && _host is not null && _petWindow is not null && _bossKey is not null && _petSwitcher is not null)
        {
            var services = _host.Services;
            _panelViewModel = new PanelViewModel(
                new SettingsTabViewModel(
                    services.GetRequiredService<SettingsService>(),
                    _bossKey,
                    services.GetRequiredService<IAutoStartService>(),
                    () => OpenFolder(_logDirectory, "The log directory could not be opened."),
                    _installedPets),
                new PetsTabViewModel(
                    services.GetRequiredService<SettingsService>(),
                    services.GetRequiredService<IPetCatalog>(),
                    services.GetRequiredService<IPetLoader>(),
                    _petSwitcher,
                    _previewFrames.Get,
                    _previewFrames.PreloadAsync,
                    () => OpenFolder(_userPetsDirectory, "The pets folder could not be opened.")));
            _ = _panelViewModel.Pets.RefreshQuietlyAsync(services.GetRequiredService<ILogger<App>>());
        }

        return _panelViewModel;
    }

    /// <summary>
    /// Opens a folder in the file manager, creating it first so there is somewhere to put things.
    /// </summary>
    /// <param name="directory">The folder to open.</param>
    /// <param name="failureMessage">What to log if it cannot be opened.</param>
    private void OpenFolder(string? directory, string failureMessage)
    {
        try
        {
            Directory.CreateDirectory(directory!);
            Process.Start(new ProcessStartInfo(directory!) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _host?.Services.GetRequiredService<ILogger<App>>().LogWarning(ex, "{Message}", failureMessage);
        }
    }
}
