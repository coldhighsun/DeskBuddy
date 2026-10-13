using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Decides which colors the user interface is shown in.
/// </summary>
public static class ThemeResolver
{
    /// <summary>
    /// Picks the palette for a theme setting.
    /// </summary>
    /// <param name="theme">
    /// The chosen theme.
    /// </param>
    /// <param name="systemUsesLightTheme">
    /// Tells whether the operating system shows applications in the light theme. Only asked when the choice is
    /// <see cref="AppTheme.Auto"/>, so an explicit choice never reads the system setting.
    /// </param>
    /// <param name="highContrast">
    /// Whether Windows is in a high-contrast theme, which always wins over the choice: the user needs those colors.
    /// </param>
    /// <returns>
    /// The palette to show.
    /// </returns>
    public static ThemePalette Resolve(AppTheme theme, Func<bool> systemUsesLightTheme, bool highContrast)
    {
        ArgumentNullException.ThrowIfNull(systemUsesLightTheme);

        if (highContrast)
        {
            return ThemePalette.HighContrast;
        }

        var dark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => !systemUsesLightTheme(),
        };

        return dark ? ThemePalette.Dark : ThemePalette.Light;
    }

    /// <summary>
    /// Gets the name of the file under <c>Themes</c> that holds the colors of a palette.
    /// </summary>
    /// <param name="palette">
    /// The palette.
    /// </param>
    /// <returns>
    /// The file name, for example <c>Colors.Dark.xaml</c>.
    /// </returns>
    public static string ColorsFile(ThemePalette palette)
    {
        return palette switch
        {
            ThemePalette.Light => "Colors.Light.xaml",
            ThemePalette.Dark => "Colors.Dark.xaml",
            ThemePalette.HighContrast => "Colors.HighContrast.xaml",
            _ => throw new ArgumentOutOfRangeException(nameof(palette), palette, "Unknown palette."),
        };
    }
}
