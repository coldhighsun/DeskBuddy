using Microsoft.Extensions.DependencyInjection;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App;

/// <summary>
/// The part of the application that switches the interface language while it runs.
/// </summary>
public partial class App
{
    /// <summary>
    /// The language of the operating system, read when the process started, before the interface language was set. It is
    /// kept because <see cref="CultureInfo.CurrentUICulture"/> holds the interface language from then on, which would make
    /// "follow the system" follow the language that was applied last.
    /// </summary>
    private readonly CultureInfo _systemCulture = CultureInfo.CurrentUICulture;

    /// <summary>
    /// The language setting the interface is written in now.
    /// </summary>
    private AppLanguage _appliedLanguage;

    /// <summary>
    /// Sets the language the interface is written in. Must run on the UI thread, as the current culture belongs to a thread.
    /// </summary>
    /// <param name="language">The language setting.</param>
    private void ApplyLanguage(AppLanguage language)
    {
        var culture = LanguageResolver.Resolve(language, _systemCulture);
        Strings.Culture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        _appliedLanguage = language;
        _host?.Services.GetRequiredService<ILogger<App>>().LogInformation("Interface language: {Language}.", culture.Name);
    }

    /// <summary>
    /// Notices a change of the language setting, which may be reported on any thread, and has it applied on the UI thread.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="settings">The new settings.</param>
    private void OnSettingsChangedForLanguage(object? sender, PetSettings settings)
    {
        var language = settings.Language;

        _ = Dispatcher.BeginInvoke(() => SwitchLanguage(language));
    }

    /// <summary>
    /// Switches the interface to a language without a restart: the culture is changed, then everything that shows text
    /// writes it again. Windows are not built anew; the text of their XAML follows through <see cref="Loc"/>, and the view
    /// models listen to <see cref="LocalizedStrings.LanguageChanged"/>.
    /// </summary>
    /// <param name="language">The language setting to switch to.</param>
    private void SwitchLanguage(AppLanguage language)
    {
        if (language == _appliedLanguage)
        {
            return;
        }

        ApplyLanguage(language);

        LocalizedStrings.Instance.NotifyLanguageChanged();

        if (_installedPets is not null && _host is not null)
        {
            // The names of the pets are written in the language they were loaded in; the tray menu follows once they are.
            _ = RefreshInstalledPetsAsync(_installedPets, _host.Services.GetRequiredService<ILogger<App>>());
        }

        _tray?.RefreshLanguage();
        _petHost?.Post(window => window.RefreshLanguage());
    }

    /// <summary>
    /// Looks at the installed pets again, and reports a failure instead of losing it with the task.
    /// </summary>
    /// <param name="pets">The installed pets.</param>
    /// <param name="logger">Reports a failure.</param>
    /// <returns>A task that completes when the pets have been looked at.</returns>
    private static async Task RefreshInstalledPetsAsync(InstalledPets pets, ILogger logger)
    {
        try
        {
            await pets.RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing waits for this, so any failure (an unreadable pets folder, say) would otherwise go unseen.
            logger.LogWarning(ex, "The pets could not be looked at again after the language changed.");
        }
    }
}
