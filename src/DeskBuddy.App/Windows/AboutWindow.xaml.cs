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
/// The about window: name, version, a short description and a link to the project page.
/// </summary>
public partial class AboutWindow
{
    /// <summary>
    /// The largest picture of the application icon, decoded the first time the window is made and shared by every window
    /// after it. It is frozen, so it may be used from any thread. A failed load is not remembered: the next window tries
    /// again instead of throwing the same error for as long as the application runs.
    /// </summary>
    private static readonly Lazy<BitmapSource> Logo = new(LoadLargestIconFrame, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Looks for updates; <see langword="null"/> if the window does not offer that.
    /// </summary>
    private readonly UpdateController? _updates;

    /// <summary>
    /// Reports what went wrong when a check for updates could not be carried out.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutWindow"/> class.
    /// </summary>
    /// <param name="updates">
    /// Looks for updates; <see langword="null"/> leaves out the button that checks for them.
    /// </param>
    /// <param name="logger">
    /// Reports what went wrong; <see langword="null"/> reports nothing.
    /// </param>
    internal AboutWindow(UpdateController? updates = null, ILogger? logger = null)
    {
        InitializeComponent();
        _updates = updates;
        _logger = logger ?? NullLogger.Instance;
        if (updates is null)
        {
            UpdatePanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            updates.Changed += OnUpdatesChanged;
            Closed += (_, _) => updates.Changed -= OnUpdatesChanged;
            ShowUpdateStatus();
        }

        VersionText.Text = string.Format(CultureInfo.CurrentCulture, Strings.AboutVersion, AboutInfo.CurrentVersion());
        LogoImage.Source = Logo.Value;
        ProjectLink.NavigateUri = AboutInfo.ProjectUri;
    }

    /// <summary>
    /// Brings the window to the front, restoring it if it was minimized.
    /// </summary>
    public void ShowAbout()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
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

        await UpdateRunner.RunAsync(() => _updates.CheckAsync(manual: true), _logger);
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
    /// Closes the window. The window is shown without <see cref="Window.ShowDialog"/>, so a button marked as the cancel
    /// button does not close it by itself.
    /// </summary>
    /// <param name="sender">
    /// The close button.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
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
