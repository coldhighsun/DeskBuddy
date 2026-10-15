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
    /// Gets or sets a value indicating whether the pet may climb the sides of the screen and hang from its top edge.
    /// </summary>
    [ObservableProperty]
    private bool _climbEdges;

    /// <summary>
    /// Gets the languages to choose from.
    /// </summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new(AppLanguage.Auto, Strings.LanguageAuto),
        new(AppLanguage.ZhCN, Strings.LanguageZhCN),
        new(AppLanguage.En, Strings.LanguageEn),
    ];

    /// <summary>
    /// Gets the themes to choose from.
    /// </summary>
    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new(AppTheme.Auto, Strings.ThemeAuto),
        new(AppTheme.Light, Strings.ThemeLight),
        new(AppTheme.Dark, Strings.ThemeDark),
    ];

    /// <summary>
    /// Applies the chosen theme; it takes effect at once.
    /// </summary>
    /// <param name="value">
    /// The theme.
    /// </param>
    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { Theme = value });
        }
    }

    /// <summary>
    /// Applies the chosen pet size.
    /// </summary>
    /// <param name="value">
    /// The size.
    /// </param>
    partial void OnSelectedSizeChanged(PetSize value)
    {
        UpdateSizeChoices(_settings.Current.PetName);
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { Size = value });
        }
    }

    /// <summary>
    /// Chooses a size, which is applied at once; what the size buttons of the tab do when they are pressed.
    /// </summary>
    /// <param name="size">
    /// The size.
    /// </param>
    [RelayCommand]
    private void ChooseSize(PetSize size)
    {
        SelectedSize = size;
    }

    /// <summary>
    /// Draws the sizes again, for the size chosen and the pet in use, if what they show has changed.
    /// </summary>
    /// <param name="petName">
    /// The folder name of the pet in use.
    /// </param>
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
    /// Applies the choice to let the pet climb along the edges of the screen.
    /// </summary>
    /// <param name="value">
    /// Whether the pet may climb along the edges.
    /// </param>
    partial void OnClimbEdgesChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { ClimbEdges = value });
        }
    }

    /// <summary>
    /// Applies the chosen language; it takes full effect after a restart.
    /// </summary>
    /// <param name="value">
    /// The language.
    /// </param>
    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { Language = value });
        }
    }

    /// <summary>
    /// Draws the sizes again when the pets that can be chosen changed, as the pet in use may have just become known.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnPetsChanged(object? sender, EventArgs e)
    {
        // Only the pictures of the sizes depend on the pets; the rest of the tab may hold entries not applied yet.
        UpdateSizeChoices(_settings.Current.PetName);
    }
}
