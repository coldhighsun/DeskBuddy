using System.Windows;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// The styles and colors for the windows that live on the thread of their own that the pet is on. The application
/// resources belong to the main UI thread, and a brush or style made there cannot be used from another thread, so the
/// pet's windows (the pet, its speech bubble) get a set of their own, made on their own thread from the same dictionaries
/// and kept in step with the palette by <see cref="Show"/>. A window takes it over with <see cref="Apply"/>.
/// </summary>
public sealed class PetThreadTheme
{
    /// <summary>
    /// Where the styles of the application are, in its own assembly.
    /// </summary>
    private static readonly Uri StylesUri = new("pack://application:,,,/DeskBuddy;component/Themes/AppTheme.xaml");

    /// <summary>
    /// Where the styles of the buttons of the speech bubble are.
    /// </summary>
    private static readonly Uri BubbleButtonUri = new("pack://application:,,,/DeskBuddy;component/Themes/BubbleButton.xaml");

    /// <summary>
    /// The theme of the thread this runs on; <see langword="null"/> on a thread that has none.
    /// </summary>
    [ThreadStatic]
    private static PetThreadTheme? _current;

    /// <summary>
    /// The resources a window merges in: the styles and, as the last one, the colors of the palette shown.
    /// </summary>
    private readonly ResourceDictionary _resources = new();

    /// <summary>
    /// The color dictionary merged into <see cref="_resources"/> now.
    /// </summary>
    private ResourceDictionary? _colors;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetThreadTheme"/> class.
    /// </summary>
    private PetThreadTheme()
    {
    }

    /// <summary>
    /// Gets the theme of the thread this is called on; <see langword="null"/> on a thread that has none.
    /// </summary>
    public static PetThreadTheme? Current => _current;

    /// <summary>
    /// Makes the theme for the thread this is called on, which then hands it to every window that asks for it.
    /// </summary>
    /// <param name="palette">The palette to show at first.</param>
    /// <returns>The theme of this thread.</returns>
    public static PetThreadTheme Install(ThemePalette palette)
    {
        var theme = new PetThreadTheme();
        theme._resources.MergedDictionaries.Add(new ResourceDictionary { Source = StylesUri });
        theme._resources.MergedDictionaries.Add(new ResourceDictionary { Source = BubbleButtonUri });
        theme.Show(palette);
        _current = theme;
        return theme;
    }

    /// <summary>
    /// Gives a window the styles and colors of the thread it lives on. Does nothing on a thread that has no theme, so a
    /// window made anywhere else (in a test, say) still works and is only unstyled.
    /// </summary>
    /// <param name="element">The window.</param>
    public static void Apply(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (_current is { } theme)
        {
            element.Resources.MergedDictionaries.Add(theme._resources);
        }
    }

    /// <summary>
    /// Shows a palette: replaces the colors that were merged in, and every window that reads them through dynamic
    /// resources follows. Call on the thread the theme belongs to.
    /// </summary>
    /// <param name="palette">The palette to show.</param>
    public void Show(ThemePalette palette)
    {
        var colors = new ResourceDictionary { Source = ThemeService.ColorsUri(palette) };
        if (_colors is not null)
        {
            _resources.MergedDictionaries.Remove(_colors);
        }

        _resources.MergedDictionaries.Add(colors);
        _colors = colors;
    }
}
