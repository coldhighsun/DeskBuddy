using System.IO;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Changes the pet's pet while the application runs: when the pet named in the settings changes, the new pet is
/// loaded and handed to the window. If it cannot be loaded the old pet stays and the setting is put back, so the
/// settings always name the pet that is on screen.
/// </summary>
public sealed class PetSwitcher : IDisposable
{
    /// <summary>
    /// The settings that name the pet.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// Loads a pet from its folder.
    /// </summary>
    private readonly IPetLoader _loader;

    /// <summary>
    /// The folder that holds one subfolder per pet.
    /// </summary>
    private readonly string _petsDirectory;

    /// <summary>
    /// Puts a loaded pet on screen; may throw if its images cannot be decoded.
    /// </summary>
    private readonly Action<Pet> _apply;

    /// <summary>
    /// Logger used to report a pet that could not be switched to.
    /// </summary>
    private readonly ILogger<PetSwitcher> _logger;

    /// <summary>
    /// Does the slow part of getting a pet ready (decoding its images) before <see cref="_apply"/>, so that it need not
    /// run on the UI thread; <see langword="null"/> if there is nothing to prepare.
    /// </summary>
    private readonly Func<Pet, Task>? _prepare;

    /// <summary>
    /// The name of the pet on screen.
    /// </summary>
    private string _currentName;

    /// <summary>
    /// The name of the pet most recently asked for, so an older, slower load does not overwrite a newer one.
    /// </summary>
    private string _requestedName;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetSwitcher"/> class and starts following the settings.
    /// </summary>
    /// <param name="settings">
    /// The settings that name the pet.
    /// </param>
    /// <param name="loader">
    /// Loads a pet from its folder.
    /// </param>
    /// <param name="petsDirectory">
    /// The folder that holds one subfolder per pet.
    /// </param>
    /// <param name="currentName">
    /// The name of the pet on screen at the start.
    /// </param>
    /// <param name="apply">
    /// Puts a loaded pet on screen; may throw if its images cannot be decoded.
    /// </param>
    /// <param name="logger">
    /// Logger used to report a pet that could not be switched to.
    /// </param>
    /// <param name="prepare">
    /// Does the slow part of getting a pet ready before it is put on screen, off the caller's thread if it can; may throw
    /// like <paramref name="apply"/> if the images cannot be decoded. <see langword="null"/> if there is nothing to prepare.
    /// </param>
    public PetSwitcher(
        SettingsService settings,
        IPetLoader loader,
        string petsDirectory,
        string currentName,
        Action<Pet> apply,
        ILogger<PetSwitcher> logger,
        Func<Pet, Task>? prepare = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings;
        _loader = loader;
        _petsDirectory = petsDirectory;
        _currentName = currentName;
        _requestedName = currentName;
        _apply = apply;
        _logger = logger;
        _prepare = prepare;

        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    /// Raised when a pet could not be switched to; the argument says why. The old pet is still on screen.
    /// </summary>
    public event EventHandler<string>? SwitchFailed;

    /// <inheritdoc />
    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>
    /// Switches to a pet if it is not the one on screen. A failure is reported through <see cref="SwitchFailed"/> and
    /// the setting is put back to the pet on screen.
    /// </summary>
    /// <param name="name">
    /// The pet's folder name.
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
            var pet = await _loader.LoadAsync(Path.Combine(_petsDirectory, name));
            if (_prepare is not null)
            {
                await _prepare(pet);
            }

            // A newer request has been made while this one was loading; let that one win.
            if (!string.Equals(name, _requestedName, StringComparison.Ordinal))
            {
                return;
            }

            _apply(pet);
            _currentName = name;
        }
        catch (Exception ex) when (PetLoadFailure.Is(ex))
        {
            _logger.LogWarning(ex, "Could not switch to the '{Pet}' pet; keeping '{Current}'.", name, _currentName);

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
    /// Follows a change of the pet name in the settings.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private async void OnSettingsChanged(object? sender, PetSettings settings)
    {
        await SwitchAsync(settings.PetName);
    }

    /// <summary>
    /// Puts the pet name in the settings back to the pet on screen.
    /// </summary>
    /// <returns>
    /// A task that completes when the setting has been restored.
    /// </returns>
    private async Task RestoreSettingAsync()
    {
        try
        {
            await _settings.UpdateAsync(settings => settings with { PetName = _currentName });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "The pet setting could not be put back to '{Current}'.", _currentName);
        }
    }
}
