using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Security;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Applies the color theme: the colors DeskBuddy draws itself, and the WPF theme of the standard controls in the windows
/// and menus that are registered. While the theme follows the system, it changes along with the system; a Windows
/// high-contrast theme always wins over the choice. The service belongs to the UI thread: create it, register elements
/// and dispose it there.
/// </summary>
public sealed class ThemeService : IDisposable
{
    /// <summary>
    /// Registry key holding the theme the operating system shows applications in.
    /// </summary>
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// Value in <see cref="PersonalizeKey"/> that is 1 for the light theme and 0 for the dark one.
    /// </summary>
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    /// <summary>
    /// Where the color dictionaries of DeskBuddy live, in its own assembly; the file name is completed with the palette.
    /// The assembly name in it must match <c>AssemblyName</c> of the project.
    /// </summary>
    private const string ColorsUriPrefix = "pack://application:,,,/DeskBuddy;component/Themes/";

    /// <summary>
    /// Where the Fluent theme resources of WPF live; the file name is completed with the theme.
    /// </summary>
    internal const string DefaultFluentUriPrefix = "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.";

    /// <summary>
    /// The resources of the application, which receive the colors DeskBuddy draws itself.
    /// </summary>
    private readonly ResourceDictionary _applicationResources;

    /// <summary>
    /// The UI thread's dispatcher, which changes made on other threads are handed to.
    /// </summary>
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Tells whether the operating system shows applications in the light theme; <see langword="null"/> when it cannot
    /// be told.
    /// </summary>
    private readonly Func<bool?> _systemUsesLightTheme;

    /// <summary>
    /// Asks whether Windows is in a high-contrast theme, and whether it could answer.
    /// </summary>
    private readonly HighContrastProbe _highContrast;

    /// <summary>
    /// Where the Fluent theme resources live; the file name is completed with the theme.
    /// </summary>
    private readonly string _fluentUriPrefix;

    /// <summary>
    /// Reports the theme settings changing.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// Reports a theme resource that could not be loaded.
    /// </summary>
    private readonly ILogger<ThemeService> _logger;

    /// <summary>
    /// The elements that follow the theme: windows get the WPF theme mode, other elements (such as a context menu) get the
    /// Fluent resources merged. Held weakly so a closed window can be collected.
    /// </summary>
    private readonly List<WeakReference<FrameworkElement>> _elements = [];

    /// <summary>
    /// The theme last applied; <see langword="null"/> until the first <see cref="Apply"/>.
    /// </summary>
    private AppTheme? _theme;

    /// <summary>
    /// The palette currently shown; <see langword="null"/> until the first <see cref="Apply"/>.
    /// </summary>
    private ThemePalette? _palette;

    /// <summary>
    /// The color dictionary currently merged into the application resources, if any.
    /// </summary>
    private ResourceDictionary? _colors;

    /// <summary>
    /// Whether the service has been disposed, after which work that was already queued on the dispatcher is dropped.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Whether the failure to read the system theme has been logged, so that it is reported once and not at every change.
    /// </summary>
    private bool _systemThemeFailureLogged;

    /// <summary>
    /// Whether the failure to read the high-contrast state has been logged.
    /// </summary>
    private bool _highContrastFailureLogged;

    /// <summary>
    /// Asks the system whether a high-contrast theme is on.
    /// </summary>
    /// <param name="on">
    /// Whether a high-contrast theme is on; <see langword="false"/> when the system could not answer.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the system answered.
    /// </returns>
    internal delegate bool HighContrastProbe(out bool on);

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class and shows the theme from the settings.
    /// </summary>
    /// <param name="applicationResources">
    /// The resources of the application, which receive the colors DeskBuddy draws itself.
    /// </param>
    /// <param name="dispatcher">
    /// The UI thread's dispatcher; a change of the settings is handed to it.
    /// </param>
    /// <param name="settings">
    /// The settings holding the theme; a change of it is applied at once.
    /// </param>
    /// <param name="logger">
    /// Reports a theme resource that could not be loaded.
    /// </param>
    public ThemeService(
        ResourceDictionary applicationResources,
        Dispatcher dispatcher,
        SettingsService settings,
        ILogger<ThemeService> logger)
        : this(applicationResources, dispatcher, settings, logger, ReadSystemUsesLightTheme, HighContrastReader.TryRead, DefaultFluentUriPrefix)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class with the parts that talk to the system replaced,
    /// so that tests can decide what the system says.
    /// </summary>
    /// <param name="applicationResources">
    /// The resources of the application, which receive the colors DeskBuddy draws itself.
    /// </param>
    /// <param name="dispatcher">
    /// The UI thread's dispatcher; a change of the settings is handed to it.
    /// </param>
    /// <param name="settings">
    /// The settings holding the theme; a change of it is applied at once.
    /// </param>
    /// <param name="logger">
    /// Reports a theme resource that could not be loaded.
    /// </param>
    /// <param name="systemUsesLightTheme">
    /// Tells whether the operating system shows applications in the light theme; <see langword="null"/> when it cannot be
    /// told.
    /// </param>
    /// <param name="highContrast">
    /// Asks whether Windows is in a high-contrast theme, and whether it could answer.
    /// </param>
    /// <param name="fluentUriPrefix">
    /// Where the Fluent theme resources live; the file name is completed with the theme.
    /// </param>
    internal ThemeService(
        ResourceDictionary applicationResources,
        Dispatcher dispatcher,
        SettingsService settings,
        ILogger<ThemeService> logger,
        Func<bool?> systemUsesLightTheme,
        HighContrastProbe highContrast,
        string fluentUriPrefix)
    {
        _applicationResources = applicationResources;
        _dispatcher = dispatcher;
        _settings = settings;
        _logger = logger;
        _systemUsesLightTheme = systemUsesLightTheme;
        _highContrast = highContrast;
        _fluentUriPrefix = fluentUriPrefix;

        Apply(settings.Current.Theme);
        settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>
    /// Gets how many elements follow the theme.
    /// </summary>
    internal int RegisteredCount => _elements.Count(reference => reference.TryGetTarget(out _));

    /// <summary>
    /// Makes a window or other element follow the theme, now and whenever it changes. The standard controls inside it
    /// take the WPF Fluent theme; elements that are not given this keep the classic look, which is what the transparent
    /// pet windows need: the application-wide theme would paint a background behind the pet.
    /// </summary>
    /// <param name="element">
    /// The window or element to theme.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Called from a thread other than the UI thread.
    /// </exception>
    public void Register(FrameworkElement element)
    {
        _dispatcher.VerifyAccess();

        if (!_elements.Any(reference => reference.TryGetTarget(out var known) && ReferenceEquals(known, element)))
        {
            _elements.Add(new WeakReference<FrameworkElement>(element));
        }

        Style(element, _palette ?? ThemePalette.Light);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    /// <summary>
    /// Reads whether the operating system shows applications in the light theme.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> for the light theme and <see langword="false"/> for the dark one; <see langword="null"/>
    /// when the setting cannot be read, for example because a policy blocks the registry key. A setting that does not
    /// exist (older Windows versions) counts as the light theme.
    /// </returns>
    private static bool? ReadSystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(AppsUseLightThemeValue) is not int value || value != 0;
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Asks whether the system uses the light theme. When it cannot say, the light theme is assumed and the problem is
    /// logged once.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> for the light theme.
    /// </returns>
    private bool SystemUsesLightTheme()
    {
        if (_systemUsesLightTheme() is { } light)
        {
            return light;
        }

        if (!_systemThemeFailureLogged)
        {
            _systemThemeFailureLogged = true;
            _logger.LogWarning("The system app theme could not be read; assuming the light theme.");
        }

        return true;
    }

    /// <summary>
    /// Asks whether Windows is in a high-contrast theme. When the system cannot answer, it is assumed not to be and the
    /// problem is logged once.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> while a high-contrast theme is on.
    /// </returns>
    private bool HighContrast()
    {
        if (_highContrast(out var on))
        {
            return on;
        }

        if (!_highContrastFailureLogged)
        {
            _highContrastFailureLogged = true;
            _logger.LogWarning("The high-contrast setting could not be read; assuming it is off.");
        }

        return false;
    }

    /// <summary>
    /// Works out which palette to show now.
    /// </summary>
    /// <returns>
    /// The palette for the chosen theme and the current state of the system.
    /// </returns>
    private ThemePalette ResolvePalette()
    {
        return ThemeResolver.Resolve(_theme ?? AppTheme.Auto, SystemUsesLightTheme, HighContrast());
    }

    /// <summary>
    /// Shows the application in a theme. Must be called on the UI thread.
    /// </summary>
    /// <param name="theme">
    /// The theme to show.
    /// </param>
    private void Apply(AppTheme theme)
    {
        if (_theme == theme)
        {
            return;
        }

        _theme = theme;
        Refresh();
    }

    /// <summary>
    /// Works out the palette and, if it differs from the one shown, passes it on to everything that follows the theme.
    /// </summary>
    internal void Refresh()
    {
        var palette = ResolvePalette();
        if (palette == _palette)
        {
            return;
        }

        _palette = palette;

        var colors = new ResourceDictionary { Source = new Uri($"{ColorsUriPrefix}{ThemeResolver.ColorsFile(palette)}") };
        var merged = _applicationResources.MergedDictionaries;
        if (_colors is not null)
        {
            merged.Remove(_colors);
        }

        merged.Add(colors);
        _colors = colors;

        _elements.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in _elements)
        {
            if (reference.TryGetTarget(out var element))
            {
                Style(element, palette);
            }
        }
    }

    /// <summary>
    /// Gives an element the WPF Fluent theme for a palette. When the Fluent resources cannot be loaded the element keeps
    /// the classic look, which still works.
    /// </summary>
    /// <param name="element">
    /// The window or element.
    /// </param>
    /// <param name="palette">
    /// The palette to show.
    /// </param>
    private void Style(FrameworkElement element, ThemePalette palette)
    {
        if (element is Window window)
        {
            // ThemeMode is still marked experimental in .NET 10; the project silences WPF0001 for it. The system mode
            // is what carries the high-contrast colors.
            window.ThemeMode = palette switch
            {
                ThemePalette.Dark => ThemeMode.Dark,
                ThemePalette.Light => ThemeMode.Light,
                _ => ThemeMode.System,
            };
            return;
        }

        var merged = element.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Source?.OriginalString.StartsWith(_fluentUriPrefix, StringComparison.Ordinal) == true)
            {
                merged.RemoveAt(i);
            }
        }

        var file = palette == ThemePalette.HighContrast ? "HC" : palette.ToString();
        try
        {
            merged.Add(new ResourceDictionary { Source = new Uri($"{_fluentUriPrefix}{file}.xaml") });
        }
        catch (Exception ex) when (ex is IOException or XamlParseException or UriFormatException)
        {
            _logger.LogWarning(ex, "The Fluent theme resources '{File}' could not be loaded; using the classic look.", file);
        }
    }

    /// <summary>
    /// Shows a theme chosen in the settings.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        // The settings change on any thread; the resources belong to the UI thread.
        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                Apply(settings.Theme);
            }
        });
    }

    /// <summary>
    /// Follows a change of the system theme or of the high-contrast setting.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// The event data.
    /// </param>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility)
        {
            _dispatcher.BeginInvoke(() =>
            {
                if (!_disposed)
                {
                    Refresh();
                }
            });
        }
    }
}
