using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AwesomeAssertions;
using DeskBuddy.App.Presentation;
using DeskBuddy.Application;
using DeskBuddy.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="ThemeService"/>. Each test runs on its own single-threaded apartment with its own resources and
/// dispatcher, so no <see cref="System.Windows.Application"/> is needed.
/// </summary>
public class ThemeServiceTests
{
    /// <summary>
    /// The pack address the real Fluent theme resources are loaded from.
    /// </summary>
    private const string FluentPrefix = ThemeService.DefaultFluentUriPrefix;

    /// <summary>
    /// The theme from the settings is shown at once: the colors are merged in and a registered window and menu follow it.
    /// </summary>
    [Theory]
    [InlineData(AppTheme.Dark, "#FF2E2620", "Dark", "Fluent.Dark.xaml")]
    [InlineData(AppTheme.Light, "#FFFDF6EC", "Light", "Fluent.Light.xaml")]
    public void Constructor_ChosenTheme_ShowsItInEverythingRegistered(AppTheme theme, string bubbleBackground, string mode, string fluentFile)
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(theme);
            var window = new Window();
            var menu = new ContextMenu();

            // Act
            fixture.Service.Register(window);
            fixture.Service.Register(menu);

            // Assert
            BubbleBackground(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString(bubbleBackground));
            window.ThemeMode.Value.Should().Be(mode);
            FluentFiles(menu).Should().Equal(fluentFile);
        });
    }

    /// <summary>
    /// A theme chosen later in the settings is shown in everything registered, replacing the old one.
    /// </summary>
    [Fact]
    public void SettingsChanged_NewTheme_SwitchesColorsWindowAndMenu()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Dark);
            var window = new Window();
            var menu = new ContextMenu();
            fixture.Service.Register(window);
            fixture.Service.Register(menu);

            // Act
            fixture.ChangeTheme(AppTheme.Light);

            // Assert
            BubbleBackground(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString("#FFFDF6EC"));
            fixture.Resources.MergedDictionaries.Count.Should().Be(1);
            window.ThemeMode.Should().Be(ThemeMode.Light);
            FluentFiles(menu).Should().Equal("Fluent.Light.xaml");
        });
    }

    /// <summary>
    /// A high-contrast theme wins over the chosen theme everywhere: its own colors, the system theme mode for windows and
    /// the high-contrast Fluent resources for menus.
    /// </summary>
    [Fact]
    public void Constructor_HighContrast_WinsOverTheChosenTheme()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Dark, highContrast: true);
            var window = new Window();
            var menu = new ContextMenu();

            // Act
            fixture.Service.Register(window);
            fixture.Service.Register(menu);

            // Assert
            fixture.Resources.MergedDictionaries.Single().Source.OriginalString.Should().EndWith("Colors.HighContrast.xaml");
            window.ThemeMode.Should().Be(ThemeMode.System);
            FluentFiles(menu).Should().Equal("Fluent.HC.xaml");
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
            BubbleBackground(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString("#FF2E2620"));
        });
    }

    /// <summary>
    /// Registering the same element again does not list it twice.
    /// </summary>
    [Fact]
    public void Register_SameElementTwice_IsListedOnce()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Dark);
            var menu = new ContextMenu();

            // Act
            fixture.Service.Register(menu);
            fixture.Service.Register(menu);

            // Assert
            fixture.Service.RegisteredCount.Should().Be(1);
            FluentFiles(menu).Should().Equal("Fluent.Dark.xaml");
        });
    }

    /// <summary>
    /// The service belongs to the UI thread: registering from another thread fails at once instead of corrupting its state.
    /// </summary>
    [Fact]
    public void Register_FromAnotherThread_Throws()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var fixture = new Fixture(AppTheme.Dark);
            var menu = new ContextMenu();

            // Act
            var register = () => Task.Run(() => fixture.Service.Register(menu)).GetAwaiter().GetResult();

            // Assert
            register.Should().Throw<InvalidOperationException>();
            fixture.Service.RegisteredCount.Should().Be(0);
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
            BubbleBackground(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString("#FF2E2620"));
        });
    }

    /// <summary>
    /// When the Fluent resources cannot be loaded, the menu keeps its classic look and a warning is logged instead of an
    /// exception escaping.
    /// </summary>
    [Fact]
    public void Register_FluentResourcesMissing_KeepsTheClassicLookAndWarns()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var logger = new RecordingLogger();
            using var fixture = new Fixture(AppTheme.Dark, logger: logger, fluentPrefix: "pack://application:,,,/NoSuchAssembly;component/Themes/Fluent.");
            var menu = new ContextMenu();

            // Act
            var register = () => fixture.Service.Register(menu);

            // Assert
            register.Should().NotThrow();
            menu.Resources.MergedDictionaries.Should().BeEmpty();
            logger.Levels.Should().Contain(LogLevel.Warning);
        });
    }

    /// <summary>
    /// The service as the application builds it, with the real Fluent address and the real system probes, themes a menu
    /// without any warning.
    /// </summary>
    [Fact]
    public void PublicConstructor_RealFluentResources_ThemeAMenuWithoutWarnings()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var settings = new SettingsService(new FakeSettingsStore());
            settings.InitializeAsync().GetAwaiter().GetResult();
            var logger = new RecordingLogger();
            var resources = new ResourceDictionary();
            using var service = new ThemeService(resources, Dispatcher.CurrentDispatcher, settings, logger);
            var menu = new ContextMenu();

            // Act
            service.Register(menu);

            // Assert
            FluentFiles(menu).Should().ContainSingle();
            resources.MergedDictionaries.Should().ContainSingle();
            logger.Levels.Should().BeEmpty();
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
            var logger = new RecordingLogger();
            using var fixture = new Fixture(AppTheme.Auto, logger: logger, systemLightTheme: null);

            // Act
            fixture.Service.Refresh();
            fixture.Service.Refresh();

            // Assert
            BubbleBackground(fixture.Resources).Should().Be((Color)ColorConverter.ConvertFromString("#FFFDF6EC"));
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
            var logger = new RecordingLogger();
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
    /// Reads the color of the bubble background out of the application resources.
    /// </summary>
    /// <param name="resources">
    /// The application resources.
    /// </param>
    /// <returns>
    /// The color.
    /// </returns>
    private static Color BubbleBackground(ResourceDictionary resources)
    {
        return ((SolidColorBrush)resources["BubbleBackgroundBrush"]).Color;
    }

    /// <summary>
    /// Lists the Fluent resource files merged into an element.
    /// </summary>
    /// <param name="element">
    /// The element.
    /// </param>
    /// <returns>
    /// The file names, such as <c>Fluent.Dark.xaml</c>.
    /// </returns>
    private static List<string> FluentFiles(FrameworkElement element)
    {
        return
        [
            .. element.Resources.MergedDictionaries
                .Select(dictionary => dictionary.Source.OriginalString)
                .Where(source => source.StartsWith(FluentPrefix, StringComparison.Ordinal))
                .Select(source => source[(FluentPrefix.Length - "Fluent.".Length)..]),
        ];
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
        /// <param name="fluentPrefix">
        /// Where the service loads the Fluent resources from; <see langword="null"/> for the real place.
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
            string? fluentPrefix = null,
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
                },
                fluentPrefix ?? FluentPrefix);
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

    /// <summary>
    /// A logger that remembers the levels it was asked to log at.
    /// </summary>
    private sealed class RecordingLogger : ILogger<ThemeService>
    {
        /// <summary>
        /// Gets the levels logged so far.
        /// </summary>
        public List<LogLevel> Levels { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
        }
    }
}
