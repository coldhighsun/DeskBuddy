using System.IO;
using AwesomeAssertions;
using DeskBuddy.App.Presentation;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="ThemeResolver"/>.
/// </summary>
public class ThemeResolverTests
{
    /// <summary>
    /// Following the system gives the dark colors exactly when the system apps use the dark theme.
    /// </summary>
    [Theory]
    [InlineData(true, ThemePalette.Light)]
    [InlineData(false, ThemePalette.Dark)]
    public void Resolve_Auto_FollowsTheSystem(bool systemUsesLightTheme, ThemePalette expected)
    {
        // Act
        var palette = ThemeResolver.Resolve(AppTheme.Auto, () => systemUsesLightTheme, highContrast: false);

        // Assert
        palette.Should().Be(expected);
    }

    /// <summary>
    /// A chosen theme wins over the system theme, and the system setting is not even read.
    /// </summary>
    [Theory]
    [InlineData(AppTheme.Light, ThemePalette.Light)]
    [InlineData(AppTheme.Dark, ThemePalette.Dark)]
    public void Resolve_ChosenTheme_IgnoresTheSystem(AppTheme theme, ThemePalette expected)
    {
        // Arrange
        var asked = false;

        // Act
        var palette = ThemeResolver.Resolve(theme, () => asked = true, highContrast: false);

        // Assert
        palette.Should().Be(expected);
        asked.Should().BeFalse();
    }

    /// <summary>
    /// A Windows high-contrast theme wins over every choice.
    /// </summary>
    [Theory]
    [InlineData(AppTheme.Auto)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void Resolve_HighContrast_WinsOverTheChoice(AppTheme theme)
    {
        // Act
        var palette = ThemeResolver.Resolve(theme, () => true, highContrast: true);

        // Assert
        palette.Should().Be(ThemePalette.HighContrast);
    }

    /// <summary>
    /// Every palette has a colors file, so a palette can never be shown without its dictionary.
    /// </summary>
    [Fact]
    public void ColorsFile_EveryPalette_HasAFileNamedForIt()
    {
        // Act
        var files = Enum.GetValues<ThemePalette>().Select(ThemeResolver.ColorsFile).ToList();

        // Assert
        files.Should().OnlyHaveUniqueItems();
        files.Should().AllSatisfy(file => File.Exists(Path.Combine(AppContext.BaseDirectory, "Themes", file)).Should().BeTrue());
    }
}
