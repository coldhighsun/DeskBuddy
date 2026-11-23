using System.Windows.Controls;
using System.Windows.Media;

namespace DeskBuddy.App.Tests.Presentation.Theme;

/// <summary>
/// Tests for <see cref="PetThreadTheme"/>, the styles and colors of the windows on the pet's own thread. Each test runs
/// on a thread of its own, which is also the thread the theme is made for.
/// </summary>
public sealed class PetThreadThemeTests
{
    /// <summary>
    /// The accent color of the light palette.
    /// </summary>
    private static readonly Color LightPrimary = (Color)ColorConverter.ConvertFromString("#FF2B6CF0");

    /// <summary>
    /// The accent color of the dark palette.
    /// </summary>
    private static readonly Color DarkPrimary = (Color)ColorConverter.ConvertFromString("#FF4F8DF7");

    /// <summary>
    /// A window given the theme reads the colors of the palette through dynamic resources.
    /// </summary>
    [Fact]
    public void Apply_AfterInstall_GivesTheElementThePaletteColors()
    {
        StaThread.Run(() =>
        {
            // Arrange
            PetThreadTheme.Install(ThemePalette.Dark);
            var element = new Border();

            // Act
            PetThreadTheme.Apply(element);

            // Assert
            ((SolidColorBrush)element.FindResource("AppPrimary")).Color.Should().Be(DarkPrimary);
        });
    }

    /// <summary>
    /// The styles of the application, a tooltip's among them, reach the element too.
    /// </summary>
    [Fact]
    public void Apply_AfterInstall_GivesTheElementTheStyles()
    {
        StaThread.Run(() =>
        {
            // Arrange
            PetThreadTheme.Install(ThemePalette.Light);
            var element = new Border();

            // Act
            PetThreadTheme.Apply(element);

            // Assert
            element.TryFindResource("BubbleButtonStyle").Should().NotBeNull();
            element.TryFindResource(typeof(ToolTip)).Should().NotBeNull();
        });
    }

    /// <summary>
    /// Showing another palette changes the colors of an element that already has the theme.
    /// </summary>
    [Fact]
    public void Show_OtherPalette_ChangesTheColorsOfElementsThatHaveTheTheme()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var theme = PetThreadTheme.Install(ThemePalette.Light);
            var element = new Border();
            PetThreadTheme.Apply(element);

            // Act
            theme.Show(ThemePalette.Dark);

            // Assert
            ((SolidColorBrush)element.FindResource("AppPrimary")).Color.Should().Be(DarkPrimary);
        });
    }

    /// <summary>
    /// Showing a palette again does not pile dictionaries up.
    /// </summary>
    [Fact]
    public void Show_CalledRepeatedly_KeepsOneColorDictionary()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var theme = PetThreadTheme.Install(ThemePalette.Light);
            var element = new Border();
            PetThreadTheme.Apply(element);
            var shared = element.Resources.MergedDictionaries.Single();
            var before = shared.MergedDictionaries.Count;

            // Act
            theme.Show(ThemePalette.Dark);
            theme.Show(ThemePalette.HighContrast);
            theme.Show(ThemePalette.Light);

            // Assert
            shared.MergedDictionaries.Count.Should().Be(before);
            ((SolidColorBrush)element.FindResource("AppPrimary")).Color.Should().Be(LightPrimary);
        });
    }

    /// <summary>
    /// On a thread without a theme an element is left as it is, so a window made in a test or anywhere else still works.
    /// </summary>
    [Fact]
    public void Apply_WithoutInstall_LeavesTheElementAlone()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var element = new Border();

            // Act
            PetThreadTheme.Apply(element);

            // Assert
            element.Resources.MergedDictionaries.Should().BeEmpty();
            PetThreadTheme.Current.Should().BeNull();
        });
    }

    /// <summary>
    /// The theme belongs to the thread it was installed on.
    /// </summary>
    [Fact]
    public void Current_AfterInstall_IsTheThemeOfThisThreadOnly()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var theme = PetThreadTheme.Install(ThemePalette.Light);
            PetThreadTheme? seenElsewhere = theme;

            // Act
            var other = new Thread(() => seenElsewhere = PetThreadTheme.Current);
            other.Start();
            other.Join();

            // Assert
            PetThreadTheme.Current.Should().BeSameAs(theme);
            seenElsewhere.Should().BeNull();
        });
    }
}
