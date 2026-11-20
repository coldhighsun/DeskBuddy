using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using DeskBuddy.App.Presentation;
using GitHubReleaseUpdater.AutoUpdate;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskBuddy.App.Windows;

/// <summary>
/// The content of the about tab of the panel: name, version, a short description, a link to the project page and the
/// check for updates.
/// </summary>
public partial class AboutView
{
    /// <summary>
    /// The largest picture of the application icon, decoded the first time a view is made and shared by every view
    /// after it. It is frozen, so it may be used from any thread. A failed load is not remembered: the next view tries
    /// again instead of throwing the same error for as long as the application runs.
    /// </summary>
    private static readonly Lazy<BitmapSource> Logo = new(LoadLargestIconFrame, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Looks for updates; <see langword="null"/> if the view does not offer that.
    /// </summary>
    private UpdateController? _updates;

    /// <summary>
    /// Reports what went wrong when a check for updates could not be carried out.
    /// </summary>
    private ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutView"/> class without the check for updates; call
    /// <see cref="Attach"/> to offer it.
    /// </summary>
    public AboutView()
    {
        InitializeComponent();
        UpdateCard.Visibility = Visibility.Collapsed;
        ShowVersion();
        LogoImage.Source = Logo.Value;
        ProjectLink.NavigateUri = AboutInfo.ProjectUri;
        ProjectLinkText.Text = AboutInfo.ProjectUri.AbsoluteUri;

        // Listening only while loaded: the tab is unloaded while another tab shows, and a view that is never shown must not be
        // reached by a notification on the wrong thread. The texts are written again when it loads, for what it missed.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Starts listening for a change of the interface language, and writes the texts in the language now in use.
    /// </summary>
    /// <param name="sender">
    /// The view.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LocalizedStrings.Instance.LanguageChanged -= OnLanguageChanged;
        LocalizedStrings.Instance.LanguageChanged += OnLanguageChanged;
        ShowVersion();
        ShowUpdateStatus();
    }

    /// <summary>
    /// Stops listening for a change of the interface language.
    /// </summary>
    /// <param name="sender">
    /// The view.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LocalizedStrings.Instance.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>
    /// Shows the version in the interface language now in use.
    /// </summary>
    private void ShowVersion()
    {
        VersionText.Text = string.Format(CultureInfo.CurrentCulture, Strings.AboutVersion, AboutInfo.CurrentVersion());
    }

    /// <summary>
    /// Writes the texts the view sets itself in the new interface language.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ShowVersion();
        ShowUpdateStatus();
    }

    /// <summary>
    /// Offers the check for updates. The view lives as long as the panel, which lives as long as the application, so it
    /// never lets go of the controller again.
    /// </summary>
    /// <param name="updates">
    /// Looks for updates; <see langword="null"/> leaves out the button that checks for them.
    /// </param>
    /// <param name="logger">
    /// Reports what went wrong; <see langword="null"/> reports nothing.
    /// </param>
    internal void Attach(UpdateController? updates, ILogger? logger = null)
    {
        _updates = updates;
        _logger = logger ?? NullLogger.Instance;
        if (updates is null)
        {
            UpdateCard.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateCard.Visibility = Visibility.Visible;
        updates.Changed += OnUpdatesChanged;
        ShowUpdateStatus();
    }

    /// <summary>
    /// Shows what the update controller is doing or found last.
    /// </summary>
    private void ShowUpdateStatus()
    {
        if (_updates is null)
        {
            return;
        }

        CheckUpdatesButton.IsEnabled = !_updates.IsBusy;
        UpdateStatusText.Text = UpdateStatusModel.For(_updates);
    }

    /// <summary>
    /// Updates the status text when the update controller changed.
    /// </summary>
    /// <param name="sender">
    /// The update controller.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnUpdatesChanged(object? sender, EventArgs e)
    {
        ShowUpdateStatus();
    }

    /// <summary>
    /// Looks for a newer version, and asks whether to install it if there is one.
    /// </summary>
    /// <param name="sender">
    /// The button.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (_updates is null)
        {
            return;
        }

        var updates = _updates;
        await UpdateRunner.RunAsync(() => updates.CheckAsync(manual: true), _logger);

        // The check only finds out; whether to install is asked in the update dialog.
        if (updates.State == UpdateState.Available && updates.Available is not null)
        {
            _ = UpdateDialog.ShowFor(updates, Window.GetWindow(this));
        }
    }

    /// <summary>
    /// Loads the largest picture of the application icon, so that it stays sharp on high-DPI screens.
    /// </summary>
    /// <returns>
    /// The largest frame of the icon, frozen.
    /// </returns>
    private static BitmapSource LoadLargestIconFrame()
    {
        var decoder = new IconBitmapDecoder(
            new Uri("pack://application:,,,/DeskBuddy;component/Assets/DeskBuddy.ico"),
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(candidate => candidate.PixelWidth).First();
        frame.Freeze();
        return frame;
    }

    /// <summary>
    /// Opens the address of the clicked link in the default browser.
    /// </summary>
    /// <param name="sender">
    /// The link.
    /// </param>
    /// <param name="e">
    /// Navigation event data.
    /// </param>
    private void OnProjectLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No browser is registered; there is nothing else the link can do.
        }
    }
}
