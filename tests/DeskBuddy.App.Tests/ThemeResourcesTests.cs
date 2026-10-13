using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using AwesomeAssertions;
using DeskBuddy.App.Presentation;

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
        var keys = ColorFiles.Append("BubbleButton.xaml").SelectMany(KeysOf).Distinct().ToList();
        var fluent = FluentKeys();

        // Act
        var colliding = keys.Where(fluent.Contains).ToList();

        // Assert
        fluent.Should().Contain("CardBorderBrush", "the reader must find the keys the Fluent theme defines, or the check proves nothing");
        keys.Should().NotBeEmpty();
        colliding.Should().BeEmpty("these keys are also defined by the Fluent theme, which would win in themed windows");
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
    /// Reads the keys of every resource the Fluent theme defines, in every theme, by loading its dictionaries on a
    /// single-threaded apartment.
    /// </summary>
    /// <returns>
    /// The exact key names.
    /// </returns>
    private static HashSet<string> FluentKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        StaThread.Run(() =>
        {
            foreach (var theme in new[] { "Light", "Dark", "HC" })
            {
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.{theme}.xaml"),
                };
                CollectKeys(dictionary, keys);
            }
        });

        return keys;
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
}
