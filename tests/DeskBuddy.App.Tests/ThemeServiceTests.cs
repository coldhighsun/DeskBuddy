using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="ThemeService"/>. Each test runs on its own single-threaded apartment with its own resources and
/// dispatcher, so no <see cref="System.Windows.Application"/> is needed.
/// </summary>
public class ThemeServiceTests
{
    /// <summary>
    /// The address of the color dictionaries and styles, in the assembly of the application.
    /// </summary>
    private const string ThemesPrefix = "pack://application:,,,/DeskBuddy;component/Themes/";

    /// <summary>
    /// The theme from the settings is shown at once: the colors of its palette are merged into the application resources.
    /// </summary>
    /// <param name="theme">
    /// The theme in the settings.
    /// </param>
    /// <param name="primary">
    /// The accent color the palette is expected to have.
    /// </param>
    [Theory]
    [InlineData(AppTheme.Dark, "#FF4F8DF7")]
    [InlineData(AppTheme.Light, "#FF2B6CF0")]
    public void Constructor_ChosenTheme_MergesItsColors(AppTheme theme, string primary)
    {
        StaThread.Run(() =>
        {
            // Arrange & Act
            using var fixture = new Fixture(theme);

            // Assert
            Primary(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString(primary));
            fixture.Resources.MergedDictionaries.Should().ContainSingle();
        });
    }

    /// <summary>
    /// A theme chosen later in the settings replaces the colors of the old one.
    /// </summary>
    [Fact]
    public void SettingsChanged_NewTheme_ReplacesTheColors()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Dark);

            // Act
            fixture.ChangeTheme(AppTheme.Light);

            // Assert
            Primary(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString("#FF2B6CF0"));
            fixture.Resources.MergedDictionaries.Count.Should().Be(1);
        });
    }

    /// <summary>
    /// A high-contrast theme wins over the chosen theme.
    /// </summary>
    [Fact]
    public void Constructor_HighContrast_WinsOverTheChosenTheme()
    {
        StaThread.Run(() =>
        {
            // Arrange & Act
            using var fixture = new Fixture(AppTheme.Dark, highContrast: true);

            // Assert
            fixture.Resources.MergedDictionaries.Single().Source.OriginalString.Should().EndWith("Colors.HighContrast.xaml");
        });
    }

    /// <summary>
    /// Changing a setting other than the theme neither reloads the colors nor asks the system anything again.
    /// </summary>
    [Fact]
    public void SettingsChanged_ThemeUnchanged_DoesNothing()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Auto);
            var colors = fixture.Resources.MergedDictionaries.Single();
            var asked = fixture.SystemAskedCount;

            // Act
            fixture.Change(settings => settings with { Size = PetSize.Large });

            // Assert
            fixture.Resources.MergedDictionaries.Single().Should().BeSameAs(colors);
            fixture.SystemAskedCount.Should().Be(asked);
        });
    }

    /// <summary>
    /// A change of the system theme is picked up while following the system, and the colors are not reloaded when the
    /// palette stays the same.
    /// </summary>
    [Fact]
    public void Auto_SystemThemeChanges_FollowsItOnlyWhenThePaletteDiffers()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Auto);
            var light = fixture.Resources.MergedDictionaries.Single();

            // Act
            fixture.Service.Refresh();
            var unchanged = fixture.Resources.MergedDictionaries.Single();
            fixture.SystemLightTheme = false;
            fixture.Service.Refresh();

            // Assert
            unchanged.Should().BeSameAs(light);
            Primary(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString("#FF4F8DF7"));
        });
    }

    /// <summary>
    /// A theme change that was queued on the dispatcher before the service was disposed is dropped, not applied to an
    /// application that is shutting down.
    /// </summary>
    [Fact]
    public void Dispose_WorkAlreadyQueued_IsDropped()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Dark);
            fixture.Change(settings => settings with { Theme = AppTheme.Light }, pump: false);

            // Act
            fixture.Service.Dispose();
            fixture.Pump();

            // Assert
            Primary(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString("#FF4F8DF7"));
        });
    }

    /// <summary>
    /// The service as the application builds it, with the real system probes, merges one color dictionary without any
    /// warning.
    /// </summary>
    [Fact]
    public void PublicConstructor_RealSystemProbes_MergesOneDictionaryWithoutWarnings()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var settings = new SettingsService(new FakeSettingsStore());
            settings.InitializeAsync().GetAwaiter().GetResult();
            var logger = new RecordingLogger<ThemeService>();
            var resources = new ResourceDictionary();

            // Act
            using var service = new ThemeService(resources, Dispatcher.CurrentDispatcher, settings, logger);

            // Assert
            resources.MergedDictionaries.Should().ContainSingle();
            logger.Levels.Should().BeEmpty();
        });
    }

    /// <summary>
    /// The three color files define the same keys: a key that is missing in one of them would show up as a missing color
    /// only when that theme is chosen.
    /// </summary>
    [Fact]
    public void ColorFiles_LightDarkAndHighContrast_DefineTheSameKeys()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var light = Keys("Colors.Light.xaml");
            var dark = Keys("Colors.Dark.xaml");
            var highContrast = Keys("Colors.HighContrast.xaml");

            // Act & Assert
            light.Should().NotBeEmpty();
            dark.Should().BeEquivalentTo(light);
            highContrast.Should().BeEquivalentTo(light);
        });
    }

    /// <summary>
    /// The control styles load without a missing resource, and define the styles the windows ask for by name.
    /// </summary>
    [Fact]
    public void AppTheme_Loads_AndDefinesTheStylesTheWindowsUse()
    {
        StaThread.Run(() =>
        {
            // Arrange & Act
            var theme = new ResourceDictionary { Source = new Uri($"{ThemesPrefix}AppTheme.xaml") };

            // Assert
            string[] names =
            [
                "DialogWindowStyle", "BorderlessWindowStyle", "RaisedButton", "FlatButton", "IconButton", "CloseIconButton",
                "AppTabControl", "SettingsGroupStyle", "SettingsCardBorderStyle", "SettingsChipBorderStyle",
                "SettingsSectionHeaderStyle", "SettingsHintStyle", "SecondaryTextStyle",
            ];
            foreach (var name in names)
            {
                theme.Contains(name).Should().BeTrue($"{name} is used by a window");
            }
        });
    }

    /// <summary>
    /// When the system theme cannot be read the light theme is assumed, and the problem is logged once, not at every
    /// refresh.
    /// </summary>
    [Fact]
    public void Auto_SystemThemeUnreadable_AssumesLightAndWarnsOnce()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var logger = new RecordingLogger<ThemeService>();
            using var fixture = new Fixture(AppTheme.Auto, logger: logger, systemLightTheme: null);

            // Act
            fixture.Service.Refresh();
            fixture.Service.Refresh();

            // Assert
            Primary(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString("#FF2B6CF0"));
            logger.Levels.Count(level => level == LogLevel.Warning).Should().Be(1);
        });
    }

    /// <summary>
    /// When the high-contrast state cannot be read it is assumed to be off, and the problem is logged once.
    /// </summary>
    [Fact]
    public void Refresh_HighContrastUnreadable_AssumesOffAndWarnsOnce()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var logger = new RecordingLogger<ThemeService>();
            using var fixture = new Fixture(AppTheme.Dark, highContrast: true, logger: logger, highContrastAnswers: false);

            // Act
            fixture.Service.Refresh();
            fixture.Service.Refresh();

            // Assert
            fixture.Resources.MergedDictionaries.Single().Source.OriginalString.Should().EndWith("Colors.Dark.xaml");
            logger.Levels.Count(level => level == LogLevel.Warning).Should().Be(1);
        });
    }

    /// <summary>
    /// The buttons of the speech bubble take the themed bubble button style.
    /// </summary>
    [Fact]
    public void BubbleWindow_SetContent_GivesTheButtonsTheBubbleButtonStyle()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Dark);
            var style = new ResourceDictionary { Source = new Uri("pack://application:,,,/DeskBuddy;component/Themes/BubbleButton.xaml") };
            fixture.Resources.MergedDictionaries.Add(style);
            var bubble = new Windows.BubbleWindow();
            var panel = (Panel)bubble.FindName("ButtonPanel");

            // With no Application in the test, the resources are put where the buttons look for them.
            panel.Resources.MergedDictionaries.Add(fixture.Resources);

            // Act
            bubble.SetContent(new BubbleContent("Stretch your legs", [new BubbleButton(DeskBuddy.Domain.Messages.MessageAction.Dismiss, "OK")]));

            // Assert
            var button = panel.Children.OfType<Button>().Single();
            button.Style.Should().BeSameAs(style["BubbleButtonStyle"]);
            bubble.Close();
        });
    }

    /// <summary>
    /// Reads the accent color out of the application resources.
    /// </summary>
    /// <param name="resources">
    /// The application resources.
    /// </param>
    /// <returns>
    /// The color.
    /// </returns>
    private static Color Primary(ResourceDictionary resources)
    {
        return ((SolidColorBrush)resources["AppPrimary"]).Color;
    }

    /// <summary>
    /// Lists the keys a color file defines.
    /// </summary>
    /// <param name="file">
    /// The file name, such as <c>Colors.Dark.xaml</c>.
    /// </param>
    /// <returns>
    /// The keys.
    /// </returns>
    private static List<string> Keys(string file)
    {
        var dictionary = new ResourceDictionary { Source = new Uri($"{ThemesPrefix}{file}") };
        return [.. dictionary.Keys.Cast<object>().Select(key => key.ToString()!)];
    }

    /// <summary>
    /// A theme service wired to fake settings and a fake system, on the current thread's dispatcher.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        /// <summary>
        /// The settings the service watches.
        /// </summary>
        private readonly SettingsService _settings = new(new FakeSettingsStore());

        /// <summary>
        /// How often the service has asked whether the system uses the light theme.
        /// </summary>
        private int _askedCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Fixture"/> class.
        /// </summary>
        /// <param name="theme">
        /// The theme in the settings.
        /// </param>
        /// <param name="highContrast">
        /// Whether the fake system is in a high-contrast theme.
        /// </param>
        /// <param name="logger">
        /// The logger given to the service; <see langword="null"/> for one that discards everything.
        /// </param>
        /// <param name="systemLightTheme">
        /// What the fake system says about the light theme; <see langword="null"/> when it cannot say.
        /// </param>
        /// <param name="highContrastAnswers">
        /// Whether the fake system can answer the high-contrast question.
        /// </param>
        public Fixture(
            AppTheme theme,
            bool highContrast = false,
            ILogger<ThemeService>? logger = null,
            bool? systemLightTheme = true,
            bool highContrastAnswers = true)
        {
            SystemLightTheme = systemLightTheme;
            HighContrastAnswers = highContrastAnswers;
            _settings.InitializeAsync().GetAwaiter().GetResult();
            Change(settings => settings with { Theme = theme }, pump: false);

            Resources = [];
            Service = new ThemeService(
                Resources,
                Dispatcher.CurrentDispatcher,
                _settings,
                logger ?? NullLogger<ThemeService>.Instance,
                () =>
                {
                    _askedCount++;
                    return SystemLightTheme;
                },
                (out bool on) =>
                {
                    on = highContrast;
                    return HighContrastAnswers;
                });
        }

        /// <summary>
        /// Gets the application resources the service writes the colors into.
        /// </summary>
        public ResourceDictionary Resources { get; }

        /// <summary>
        /// Gets the service under test.
        /// </summary>
        public ThemeService Service { get; }

        /// <summary>
        /// Gets or sets what the fake system says about the light theme; <see langword="null"/> when it cannot say.
        /// </summary>
        public bool? SystemLightTheme { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the fake system can answer the high-contrast question.
        /// </summary>
        public bool HighContrastAnswers { get; set; } = true;

        /// <summary>
        /// Gets how often the service has asked whether the system uses the light theme.
        /// </summary>
        public int SystemAskedCount => _askedCount;

        /// <summary>
        /// Changes the theme in the settings and lets the service handle it.
        /// </summary>
        /// <param name="theme">
        /// The new theme.
        /// </param>
        public void ChangeTheme(AppTheme theme)
        {
            Change(settings => settings with { Theme = theme });
        }

        /// <summary>
        /// Changes the settings and lets the service handle it.
        /// </summary>
        /// <param name="change">
        /// The change.
        /// </param>
        /// <param name="pump">
        /// Whether to run the work queued on the dispatcher afterwards.
        /// </param>
        public void Change(Func<PetSettings, PetSettings> change, bool pump = true)
        {
            _settings.UpdateAsync(change).GetAwaiter().GetResult();
            if (pump)
            {
                Pump();
            }
        }

        /// <summary>
        /// Runs everything queued on the dispatcher so far: the service hands its work to the dispatcher.
        /// </summary>
        public void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Service.Dispose();
        }
    }
}
