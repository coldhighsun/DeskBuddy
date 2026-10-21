using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for the theme dictionaries under <c>Themes</c>.
/// </summary>
public partial class ThemeResourcesTests
{
    /// <summary>
    /// The color dictionaries, one per palette.
    /// </summary>
    private static readonly string[] ColorFiles = [.. Enum.GetValues<ThemePalette>().Select(ThemeResolver.ColorsFile)];

    /// <summary>
    /// Every palette defines the same keys, so switching the theme never leaves a brush without a value.
    /// </summary>
    [Fact]
    public void ColorDictionaries_DefineTheSameKeys()
    {
        // Act
        var first = KeysOf(ColorFiles[0]);

        // Assert
        first.Should().NotBeEmpty();
        foreach (var file in ColorFiles.Skip(1))
        {
            KeysOf(file).Should().BeEquivalentTo(first, because: file);
        }
    }

    /// <summary>
    /// A key that the WPF Fluent theme also defines would be silently overridden inside a themed window, because the
    /// window's own resources come before the application's. This is how a pet card border once went missing.
    /// </summary>
    [Fact]
    public void ThemeKeys_DoNotCollideWithFluentResources()
    {
        // Arrange
        var keys = ColorFiles.Append("BubbleButton.xaml").Append("SecondaryText.xaml").SelectMany(KeysOf).Distinct().ToList();
        var fluent = FluentKeysByTheme().Values.SelectMany(keys => keys).ToHashSet(StringComparer.Ordinal);

        // Act
        var colliding = keys.Where(fluent.Contains).ToList();

        // Assert
        fluent.Should().Contain("CardBorderBrush", "the reader must find the keys the Fluent theme defines, or the check proves nothing");
        keys.Should().NotBeEmpty();
        colliding.Should().BeEmpty("these keys are also defined by the Fluent theme, which would win in themed windows");
    }

    /// <summary>
    /// Every resource a window or theme file looks up by name is defined, either by DeskBuddy's own files or by the Fluent
    /// theme, and a Fluent one in each of its themes (light, dark and high contrast). A key that a future .NET version
    /// renames or drops from one theme would otherwise resolve to nothing there and quietly lose its color.
    /// </summary>
    [Fact]
    public void WindowMarkup_DynamicResourceKeys_AreDefined()
    {
        // Arrange
        var markup = Directory
            .EnumerateFiles(RepositoryPaths.Combine("src", "DeskBuddy.App", "Windows"), "*.xaml")
            .Concat(Directory.EnumerateFiles(RepositoryPaths.Combine("src", "DeskBuddy.App", "Themes"), "*.xaml"))
            .Select(File.ReadAllText);
        var used = markup
            .SelectMany(text => DynamicResourcePattern().Matches(text).Select(match => match.Groups[1].Value))
            .Distinct()
            .ToList();
        var own = ColorFiles.Append("BubbleButton.xaml").Append("SecondaryText.xaml").SelectMany(KeysOf).ToHashSet(StringComparer.Ordinal);
        var fluent = FluentKeysByTheme();

        // Act
        var missing = fluent
            .SelectMany(theme => used.Where(key => !own.Contains(key) && !theme.Value.Contains(key)).Select(key => $"{key} in {theme.Key}"))
            .ToList();

        // Assert
        used.Should().Contain("TextFillColorSecondaryBrush");
        missing.Should().BeEmpty("these keys are neither in DeskBuddy's own files nor in that Fluent theme");
    }

    /// <summary>
    /// Reads the resource keys a theme dictionary defines.
    /// </summary>
    /// <param name="file">
    /// The file name under <c>Themes</c>.
    /// </param>
    /// <returns>
    /// The values of the <c>x:Key</c> attributes.
    /// </returns>
    private static IEnumerable<string> KeysOf(string file)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Themes", file));
        return KeyPattern().Matches(text).Select(match => match.Groups[1].Value);
    }

    /// <summary>
    /// Reads the keys of every resource the Fluent theme defines, theme by theme, by loading its dictionaries on a
    /// single-threaded apartment.
    /// </summary>
    /// <returns>
    /// The exact key names of each Fluent theme, by the theme's name.
    /// </returns>
    private static Dictionary<string, HashSet<string>> FluentKeysByTheme()
    {
        var themes = new Dictionary<string, HashSet<string>>();
        StaThread.Run(() =>
        {
            foreach (var theme in new[] { "Light", "Dark", "HC" })
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.{theme}.xaml"),
                };
                CollectKeys(dictionary, keys);
                themes[theme] = keys;
            }
        });

        return themes;
    }

    /// <summary>
    /// Adds the string keys of a dictionary and of all the dictionaries merged into it.
    /// </summary>
    /// <param name="dictionary">
    /// The dictionary to read.
    /// </param>
    /// <param name="keys">
    /// Receives the keys.
    /// </param>
    private static void CollectKeys(ResourceDictionary dictionary, HashSet<string> keys)
    {
        foreach (var key in dictionary.Keys)
        {
            if (key is string name)
            {
                keys.Add(name);
            }
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            CollectKeys(merged, keys);
        }
    }

    /// <summary>
    /// Matches the <c>x:Key</c> attributes of a XAML dictionary.
    /// </summary>
    /// <returns>
    /// The pattern, with the key in the first group.
    /// </returns>
    [GeneratedRegex("x:Key=\"([^\"]+)\"")]
    private static partial Regex KeyPattern();

    /// <summary>
    /// Matches a <c>DynamicResource</c> that names a key directly; the ones that look up a static member of a class such as
    /// <c>SystemColors</c> are left out.
    /// </summary>
    /// <returns>
    /// The pattern, with the key in the first group.
    /// </returns>
    [GeneratedRegex(@"\{DynamicResource\s+([A-Za-z_][A-Za-z0-9_]*)\s*\}")]
    private static partial Regex DynamicResourcePattern();
}
