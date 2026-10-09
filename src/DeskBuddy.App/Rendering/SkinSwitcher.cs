using System.IO;
using DeskBuddy.Application;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.Skins;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Changes the pet's skin while the application runs: when the skin named in the settings changes, the new skin is
/// loaded and handed to the window. If it cannot be loaded the old skin stays and the setting is put back, so the
/// settings always name the skin that is on screen.
/// </summary>
public sealed class SkinSwitcher : IDisposable
{
    /// <summary>
    /// The settings that name the skin.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// Loads a skin from its folder.
    /// </summary>
    private readonly ISkinLoader _loader;

    /// <summary>
    /// The folder that holds one subfolder per skin.
    /// </summary>
    private readonly string _skinsDirectory;

    /// <summary>
    /// Puts a loaded skin on screen; may throw if its images cannot be decoded.
    /// </summary>
    private readonly Action<Skin> _apply;

    /// <summary>
    /// Logger used to report a skin that could not be switched to.
    /// </summary>
    private readonly ILogger<SkinSwitcher> _logger;

    /// <summary>
    /// The name of the skin on screen.
    /// </summary>
    private string _currentName;

    /// <summary>
    /// The name of the skin most recently asked for, so an older, slower load does not overwrite a newer one.
    /// </summary>
    private string _requestedName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkinSwitcher"/> class and starts following the settings.
    /// </summary>
    /// <param name="settings">
    /// The settings that name the skin.
    /// </param>
    /// <param name="loader">
    /// Loads a skin from its folder.
    /// </param>
    /// <param name="skinsDirectory">
    /// The folder that holds one subfolder per skin.
    /// </param>
    /// <param name="currentName">
    /// The name of the skin on screen at the start.
    /// </param>
    /// <param name="apply">
    /// Puts a loaded skin on screen; may throw if its images cannot be decoded.
    /// </param>
    /// <param name="logger">
    /// Logger used to report a skin that could not be switched to.
    /// </param>
    public SkinSwitcher(
        SettingsService settings,
        ISkinLoader loader,
        string skinsDirectory,
        string currentName,
        Action<Skin> apply,
        ILogger<SkinSwitcher> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings;
        _loader = loader;
        _skinsDirectory = skinsDirectory;
        _currentName = currentName;
        _requestedName = currentName;
        _apply = apply;
        _logger = logger;

        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    /// Raised when a skin could not be switched to; the argument says why. The old skin is still on screen.
    /// </summary>
    public event EventHandler<string>? SwitchFailed;

    /// <inheritdoc />
    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>
    /// Switches to a skin if it is not the one on screen. A failure is reported through <see cref="SwitchFailed"/> and
    /// the setting is put back to the skin on screen.
    /// </summary>
    /// <param name="name">
    /// The skin's folder name.
    /// </param>
    /// <returns>
    /// A task that completes when the switch has been made or has failed.
    /// </returns>
    public async Task SwitchAsync(string name)
    {
        if (string.Equals(name, _requestedName, StringComparison.Ordinal))
        {
            return;
        }

        _requestedName = name;
        string? failure = null;
        try
        {
            var skin = await _loader.LoadAsync(Path.Combine(_skinsDirectory, name));

            // A newer request has been made while this one was loading; let that one win.
            if (!string.Equals(name, _requestedName, StringComparison.Ordinal))
            {
                return;
            }

            _apply(skin);
            _currentName = name;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or FormatException)
        {
            _logger.LogWarning(ex, "Could not switch to the '{Skin}' skin; keeping '{Current}'.", name, _currentName);

            // A failure of a request that has been superseded no longer matters.
            if (string.Equals(name, _requestedName, StringComparison.Ordinal))
            {
                failure = ex.Message;
            }
        }

        if (failure is not null)
        {
            _requestedName = _currentName;
            await RestoreSettingAsync();
            SwitchFailed?.Invoke(this, failure);
        }
    }

    /// <summary>
    /// Follows a change of the skin name in the settings.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private async void OnSettingsChanged(object? sender, PetSettings settings)
    {
        await SwitchAsync(settings.SkinName);
    }

    /// <summary>
    /// Puts the skin name in the settings back to the skin on screen.
    /// </summary>
    /// <returns>
    /// A task that completes when the setting has been restored.
    /// </returns>
    private async Task RestoreSettingAsync()
    {
        try
        {
            await _settings.UpdateAsync(settings => settings with { SkinName = _currentName });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "The skin setting could not be put back to '{Current}'.", _currentName);
        }
    }
}
