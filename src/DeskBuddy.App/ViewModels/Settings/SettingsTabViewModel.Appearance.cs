using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.App.ViewModels;

/// <content>
/// Language, theme, pet size and edge climbing.
/// </content>
public sealed partial class SettingsTabViewModel
{
    /// <summary>
    /// What <see cref="SizeChoices"/> was made for: the pet in use, the size in use and the version of the pet list.
    /// Only a change in one of them needs new choices, which rebuilds the buttons that show them.
    /// </summary>
    private (string PetName, PetSize Size, int PetsVersion)? _sizeChoicesFor;

    /// <summary>
    /// Gets or sets the language of the user interface.
    /// </summary>
    [ObservableProperty]
    private AppLanguage _selectedLanguage;

    /// <summary>
    /// Gets or sets the color theme of the user interface.
    /// </summary>
    [ObservableProperty]
    private AppTheme _selectedTheme;

    /// <summary>
    /// Gets or sets the animation played when another pet is switched to.
    /// </summary>
    [ObservableProperty]
    private PetSwitchEffect _selectedSwitchEffect;

    /// <summary>
    /// Gets or sets how large the pet is shown.
    /// </summary>
    [ObservableProperty]
    private PetSize _selectedSize;

    /// <summary>
    /// Gets or sets the sizes to choose from, each drawn with the pet in use at the size it gets on screen.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SizeChoice> _sizeChoices = [];

    /// <summary>
    /// Gets the languages to choose from.
    /// </summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        .. Enum.GetValues<AppLanguage>().Select(language => new LanguageOption(language, LanguageLabel(language))),
    ];

    /// <summary>
    /// Gets the themes to choose from.
    /// </summary>
    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        .. Enum.GetValues<AppTheme>().Select(theme => new ThemeOption(theme, ThemeLabel(theme))),
    ];

    /// <summary>
    /// Gets the switch animations to choose from.
    /// </summary>
    public IReadOnlyList<PetSwitchEffectOption> SwitchEffects { get; } =
    [
        .. Enum.GetValues<PetSwitchEffect>().Select(effect => new PetSwitchEffectOption(effect, SwitchEffectLabel(effect))),
    ];

    /// <summary>
    /// Gets or sets a value indicating whether the pet is changed for another one by itself.
    /// </summary>
    [ObservableProperty]
    private bool _autoSwitchPet;

    /// <summary>
    /// Gets or sets the interval between two automatic changes.
    /// </summary>
    [ObservableProperty]
    private TimeSpan _selectedAutoSwitchInterval = PetSettings.AutoSwitchIntervals[0];

    /// <summary>
    /// Gets the intervals to choose from, the ones <see cref="PetSettings.AutoSwitchIntervals"/> allows.
    /// </summary>
    public IReadOnlyList<AutoSwitchIntervalOption> AutoSwitchIntervals { get; } =
    [
        .. PetSettings.AutoSwitchIntervals.Select(interval => new AutoSwitchIntervalOption(interval, AutoSwitchIntervalLabel(interval))),
    ];

    /// <summary>
    /// Gets the name of an interval as the list of intervals shows it.
    /// </summary>
    /// <param name="interval">The interval.</param>
    /// <returns>The name, in whole hours if it is one, otherwise in minutes, in the interface language now in use.</returns>
    private static string AutoSwitchIntervalLabel(TimeSpan interval)
    {
        return interval.TotalMinutes % 60 == 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.AutoSwitchHours, (int)interval.TotalHours)
            : string.Format(CultureInfo.CurrentCulture, Strings.AutoSwitchMinutes, (int)interval.TotalMinutes);
    }

    /// <summary>
    /// Records the choice to change the pet by itself in the draft; it is applied when the settings are saved.
    /// </summary>
    /// <param name="value">Whether the pet is changed.</param>
    partial void OnAutoSwitchPetChanged(bool value)
    {
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.AutoSwitchPet), settings => settings with { AutoSwitchPet = value });
        }
    }

    /// <summary>
    /// Records the chosen interval in the draft; it is applied when the settings are saved.
    /// </summary>
    /// <param name="value">The interval.</param>
    partial void OnSelectedAutoSwitchIntervalChanged(TimeSpan value)
    {
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.AutoSwitchPetInterval), settings => settings with { AutoSwitchPetInterval = value });
        }
    }

    /// <summary>
    /// Gets the name of a switch animation as the list of animations shows it.
    /// </summary>
    /// <param name="effect">The animation.</param>
    /// <returns>The name, in the interface language now in use.</returns>
    private static string SwitchEffectLabel(PetSwitchEffect effect)
    {
        return effect switch
        {
            PetSwitchEffect.Ufo => Strings.SwitchEffectUfo,
            PetSwitchEffect.Claw => Strings.SwitchEffectClaw,
            PetSwitchEffect.Smoke => Strings.SwitchEffectSmoke,
            PetSwitchEffect.Beam => Strings.SwitchEffectBeam,
            _ => Strings.SwitchEffectRandom,
        };
    }

    /// <summary>
    /// Records the chosen switch animation in the draft; it is applied when the settings are saved.
    /// </summary>
    /// <param name="value">The animation.</param>
    partial void OnSelectedSwitchEffectChanged(PetSwitchEffect value)
    {
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.SwitchEffect), settings => settings with { SwitchEffect = value });
        }
    }

    /// <summary>
    /// Gets the name of a language as the list of languages shows it.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The name, in the interface language now in use.</returns>
    private static string LanguageLabel(AppLanguage language)
    {
        return language switch
        {
            AppLanguage.ZhCN => Strings.LanguageZhCN,
            AppLanguage.ZhHant => Strings.LanguageZhHant,
            AppLanguage.En => Strings.LanguageEn,
            _ => Strings.LanguageAuto,
        };
    }

    /// <summary>
    /// Gets the name of a theme as the list of themes shows it.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The name, in the interface language now in use.</returns>
    private static string ThemeLabel(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => Strings.ThemeLight,
            AppTheme.Dark => Strings.ThemeDark,
            _ => Strings.ThemeAuto,
        };
    }

    /// <summary>
    /// Applies the chosen theme; it takes effect at once.
    /// </summary>
    /// <param name="value">The theme.</param>
    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.Theme), settings => settings with { Theme = value });
        }
    }

    /// <summary>
    /// Applies the chosen pet size.
    /// </summary>
    /// <param name="value">The size.</param>
    partial void OnSelectedSizeChanged(PetSize value)
    {
        UpdateSizeChoices(_editor.Draft.PetName);
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.Size), settings => settings with { Size = value });
        }
    }

    /// <summary>
    /// Chooses a size, which is applied at once; what the size buttons of the tab do when they are pressed.
    /// </summary>
    /// <param name="size">The size.</param>
    [RelayCommand]
    private void ChooseSize(PetSize size)
    {
        SelectedSize = size;
    }

    /// <summary>
    /// Draws the sizes again, for the size chosen and the pet in use, if what they show has changed.
    /// </summary>
    /// <param name="petName">The folder name of the pet in use.</param>
    private void UpdateSizeChoices(string petName)
    {
        var key = (petName, SelectedSize, PetsVersion: _pets?.Version ?? 0);
        if (key == _sizeChoicesFor)
        {
            return;
        }

        _sizeChoicesFor = key;
        SizeChoices = PetSizeChoices.For(_pets?.Entries ?? [], petName, SelectedSize);
    }

    /// <summary>
    /// Records the chosen language in the draft; it is applied when the settings are saved.
    /// </summary>
    /// <param name="value">The language.</param>
    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.Language), settings => settings with { Language = value });
        }
    }

    /// <summary>
    /// Draws the sizes again when the pets that can be chosen changed, as the pet in use may have just become known.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private void OnPetsChanged(object? sender, EventArgs e)
    {
        // Only the pictures of the sizes depend on the pets; the rest of the tab may hold entries not applied yet.
        UpdateSizeChoices(_editor.Draft.PetName);
    }
}
