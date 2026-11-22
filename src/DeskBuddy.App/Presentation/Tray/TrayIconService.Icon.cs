using Icon = System.Drawing.Icon;
using H.NotifyIcon;

namespace DeskBuddy.App;

/// <summary>
/// The part of the tray icon service that keeps the notification-area icon in step with the pet in use.
/// </summary>
public sealed partial class TrayIconService
{
    /// <summary>
    /// Looks for pets again and builds the menu again if that changed the list. Never throws, as it is started without
    /// anything waiting for it: a failure only leaves the list as it was.
    /// </summary>
    /// <returns>A task that completes when the pets have been looked at.</returns>
    private async Task RefreshPetsAsync()
    {
        try
        {
            if (await _pets.RefreshAsync())
            {
                RebuildIfNeeded();
            }

            UpdateIcon(_pets.Entries);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The pets of the tray menu could not be listed.");
        }
        finally
        {
            ShowIcon();
        }
    }

    /// <summary>
    /// Puts the icon in the notification area, the first time it is called, showing the pet in use if it is known by now
    /// (and the application icon if the pets could not be listed).
    /// </summary>
    private void ShowIcon()
    {
        if (_iconShown || _disposed)
        {
            return;
        }

        UpdateIcon(_pets.Entries);
        _icon.ForceCreate();
        _iconShown = true;
    }

    /// <summary>
    /// Puts the icon in the notification area as soon as the pet in use is loaded, without waiting for the other pets to be
    /// loaded: only that pet is needed for the icon. If it cannot be loaded here, the icon is put there when the whole list
    /// has been looked at. Never throws, as it is started without anything waiting for it.
    /// </summary>
    /// <returns>
    /// A task that completes when the icon has been put in the notification area or it was found that it cannot be yet.
    /// </returns>
    private async Task ShowIconForPetInUseAsync()
    {
        try
        {
            var entry = await _pets.LoadAsync(_settings.Current.PetName);
            if (entry is not null && !_iconShown && !_disposed)
            {
                UpdateIcon([entry]);
                _icon.ForceCreate();
                _iconShown = true;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The tray icon could not be made from the pet in use ahead of the other pets.");
        }
    }

    /// <summary>
    /// Makes the notification-area icon show the pet in use (the icon picture the pet brings, else its idle picture), or the
    /// application icon if that pet has no picture; does nothing if the icon already shows the picture of that pet. Never
    /// throws: if the picture cannot be made into an icon, the icon stays as it was.
    /// </summary>
    /// <param name="entries">
    /// The pets to take the pet in use from: all of them, or only the pet in use while the others are still being loaded.
    /// </param>
    private void UpdateIcon(IReadOnlyList<InstalledPet> entries)
    {
        var choice = TrayIconModel.Choose(entries, _settings.Current.PetName, PetSettings.DefaultPetName);
        var picture = choice.Picture;
        if (ReferenceEquals(picture, _iconPicture))
        {
            return;
        }

        try
        {
            var icon = picture is null ? null : choice.IsIconPicture ? AppIcon.FromIconPicture(picture) : AppIcon.FromPicture(picture);
            _icon.Icon = icon ?? _appIcon;
            _petIcon?.Dispose();
            _petIcon = icon;
            _iconPicture = picture;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The tray icon could not be made from the picture of the pet.");
        }
    }
}
