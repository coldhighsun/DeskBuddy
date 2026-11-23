using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.Resources;
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
            _panel = new PanelWindow(viewModel, _updates, _updateLogger, _heap);
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
        if (_panelViewModel is null && _host is not null && _petHost is not null && _bossKey is not null && _petSwitcher is not null)
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
                    () => OpenFolder(_userPetsDirectory, "The pets folder could not be opened."),
                    _previewFrames.Retain,
                    ConfirmPetDeletion));
            _ = _panelViewModel.Pets.RefreshQuietlyAsync(services.GetRequiredService<ILogger<App>>());
        }

        return _panelViewModel;
    }

    /// <summary>
    /// Asks the user whether to delete a pet.
    /// </summary>
    /// <param name="card">The pet.</param>
    /// <param name="permanently">
    /// <see langword="true"/> if the question is about deleting for good because the Recycle Bin cannot be used.
    /// </param>
    /// <returns><see langword="true"/> if the user agrees.</returns>
    private bool ConfirmPetDeletion(PetCardViewModel card, bool permanently)
    {
        var message = string.Format(
            CultureInfo.CurrentCulture,
            permanently ? Strings.PetsDeleteConfirmPermanent : Strings.PetsDeleteConfirm,
            card.DisplayName);
        return Dialogs.Show(_panel, Strings.AppName, message, DialogKind.Warning, DialogModel.YesNoButtons()) == DialogChoice.Yes;
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
