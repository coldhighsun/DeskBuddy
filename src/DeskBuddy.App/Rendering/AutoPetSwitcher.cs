using System.Windows.Threading;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Application.Settings;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Changes the pet for another installed one by itself when the <see cref="AutoSwitchPolicy"/> says it is time, by
/// changing the pet in the settings, which <see cref="PetSwitcher"/> then carries out with the switch animation. It
/// never names a pet: the new one is any other pet that can be loaded, and a pet that cannot be loaded is skipped.
/// </summary>
public sealed class AutoPetSwitcher
{
    /// <summary>
    /// How often the policy is asked; changes are hours apart, so this only has to be often enough to catch a quiet moment.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Decides when a change is due.
    /// </summary>
    private readonly AutoSwitchPolicy _policy;

    /// <summary>
    /// The settings in which the pet is changed.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// Lists the installed pets.
    /// </summary>
    private readonly IPetCatalog _catalog;

    /// <summary>
    /// Loads a pet, to check that it can be shown before it is chosen.
    /// </summary>
    private readonly IPetLoader _loader;

    /// <summary>
    /// Tells whether a change would interrupt anything right now.
    /// </summary>
    private readonly Func<bool> _canSwitch;

    /// <summary>
    /// The random source for choosing the pet.
    /// </summary>
    private readonly Random _random;

    /// <summary>
    /// Reports what was done.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The timer that asks the policy.
    /// </summary>
    private readonly DispatcherTimer _timer;

    /// <summary>
    /// The pet the settings named last, to tell a change of pet from any other change of the settings.
    /// </summary>
    private string _petName;

    /// <summary>
    /// Whether a change is being prepared, so a slow check of the pets is not started twice.
    /// </summary>
    private bool _busy;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoPetSwitcher"/> class and starts asking the policy.
    /// </summary>
    /// <param name="policy">Decides when a change is due.</param>
    /// <param name="settings">The settings in which the pet is changed.</param>
    /// <param name="catalog">Lists the installed pets.</param>
    /// <param name="loader">Loads a pet, to check that it can be shown before it is chosen.</param>
    /// <param name="canSwitch">Tells whether a change would interrupt anything right now.</param>
    /// <param name="random">The random source for choosing the pet.</param>
    /// <param name="logger">Reports what was done.</param>
    public AutoPetSwitcher(
        AutoSwitchPolicy policy,
        SettingsService settings,
        IPetCatalog catalog,
        IPetLoader loader,
        Func<bool> canSwitch,
        Random random,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(canSwitch);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(logger);

        _policy = policy;
        _settings = settings;
        _catalog = catalog;
        _loader = loader;
        _canSwitch = canSwitch;
        _random = random;
        _logger = logger;
        _petName = settings.Current.PetName;
        _settings.Changed += OnSettingsChanged;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = CheckInterval };
        _timer.Tick += async (_, _) => await OnTickAsync();
        _timer.Start();
    }

    /// <summary>
    /// Stops asking the policy.
    /// </summary>
    public void Stop()
    {
        _timer.Stop();
        _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>
    /// Asks the policy and, if a change is due, makes it.
    /// </summary>
    /// <returns>A task that completes when the check is over.</returns>
    internal async Task OnTickAsync()
    {
        if (_busy || !_policy.Update(_canSwitch()))
        {
            return;
        }

        _busy = true;
        try
        {
            var current = _settings.Current.PetName;
            var next = await ChooseAsync(current);
            if (next is null)
            {
                _policy.Postpone();
                return;
            }

            _logger.LogInformation("Changing the pet from '{From}' to '{To}' by itself.", current, next);
            await _settings.UpdateAsync(settings => settings with { PetName = next });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A change that fails is tried again at the next interval, never taken as a reason to stop the application.
            _logger.LogWarning(ex, "Changing the pet by itself failed.");
            _policy.Postpone();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// Chooses a pet other than the current one that can be loaded; one that cannot is left out and logged.
    /// </summary>
    /// <param name="current">The folder name of the pet on screen.</param>
    /// <returns>The folder name, or <see langword="null"/> if no other pet can be shown.</returns>
    private async Task<string?> ChooseAsync(string current)
    {
        // Listing the folders reads the disk, which should not hold up the UI thread.
        var candidates = new List<string>(await Task.Run(_catalog.GetPetNames));
        while (AutoSwitchPolicy.PickNext(candidates, current, _random) is { } name)
        {
            try
            {
                await _loader.LoadAsync(_catalog.GetPetDirectory(name));
                return name;
            }
            catch (Exception ex) when (PetLoadFailure.Is(ex))
            {
                _logger.LogWarning(ex, "The '{Pet}' pet cannot be loaded and is not changed to.", name);
                candidates.Remove(name);
            }
        }

        return null;
    }

    /// <summary>
    /// Starts the wait afresh when the pet was changed, by the user or by this class, so a pet is never taken away soon after
    /// it was chosen.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="settings">The new settings.</param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        if (!string.Equals(settings.PetName, _petName, StringComparison.Ordinal))
        {
            _petName = settings.PetName;
            _policy.Postpone();
        }
    }
}
