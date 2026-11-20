using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using DeskBuddy.App.Presentation;
using GitHubReleaseUpdater;
using GitHubReleaseUpdater.AutoUpdate;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Tells the user that a newer release exists and, if they agree, downloads its installer with a progress bar and starts
/// it. It goes through three states: asking, downloading, and showing why an attempt failed. Use
/// <see cref="ShowFor"/> to show it.
/// </summary>
public partial class UpdateDialog : Window
{
    /// <summary>
    /// The update this dialog offers.
    /// </summary>
    private readonly AvailableUpdate _update;

    /// <summary>
    /// The controller that downloads and starts the installer and remembers a skipped version.
    /// </summary>
    private readonly UpdateController _updates;

    /// <summary>
    /// Whether the installer is downloaded and started by this dialog; without one the user is sent to the release page.
    /// </summary>
    private readonly bool _hasInstaller;

    /// <summary>
    /// Cancels the download that is running, or <see langword="null"/> when none is.
    /// </summary>
    private CancellationTokenSource? _downloadCancellation;

    /// <summary>
    /// What the dialog is showing now.
    /// </summary>
    private UpdateDialogState _state;

    /// <summary>
    /// Whether the window has closed, so that a download that ends late does not touch it.
    /// </summary>
    private bool _closed;

    /// <summary>
    /// Whether a progress report is already waiting to be shown, so a fast download does not queue one per chunk.
    /// </summary>
    private int _progressPending;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDialog"/> class.
    /// </summary>
    /// <param name="updates">
    /// The controller that downloads and starts the installer.
    /// </param>
    /// <param name="update">
    /// The update to offer.
    /// </param>
    internal UpdateDialog(UpdateController updates, AvailableUpdate update)
    {
        InitializeComponent();
        _updates = updates;
        _update = update;
        _hasInstaller = updates.HasInstaller;

        HeaderText.Text = string.Format(CultureInfo.CurrentCulture, Strings.UpdateAvailable, update.Version);
        CurrentVersionText.Text = string.Format(CultureInfo.CurrentCulture, Strings.UpdateCurrentVersion, AboutInfo.CurrentVersion());
        var notes = UpdateDialogModel.TrimNotes(update.Release.Body);
        if (notes.Length > 0)
        {
            NotesText.Text = notes;
            NotesPanel.Visibility = Visibility.Visible;
        }

        InfoText.Text = _hasInstaller
            ? Strings.UpdateInstallNote
            : string.Format(CultureInfo.CurrentCulture, Strings.UpdateNoInstaller, update.Version);
        SetState(UpdateDialogState.Prompt);

        updates.ProgressChanged += OnProgressChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// Gets a value indicating whether an update dialog is open. Only one is shown at a time, so a click on the notice and
    /// one on the about page cannot stack two of them.
    /// </summary>
    internal static bool IsShowing { get; private set; }

    /// <summary>
    /// Shows the dialog for the update the controller has found, unless one is open already or nothing was found. Must be
    /// used on the UI thread.
    /// </summary>
    /// <param name="updates">
    /// The controller that found the update.
    /// </param>
    /// <param name="owner">
    /// The window the dialog belongs to; <see langword="null"/> centers it on the screen.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the installer was started, which ends the application.
    /// </returns>
    internal static bool ShowFor(UpdateController updates, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(updates);

        if (IsShowing || updates.Available is not { } update)
        {
            return false;
        }

        IsShowing = true;
        try
        {
            var dialog = new UpdateDialog(updates, update);
            if (owner is { IsVisible: true })
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            return dialog.ShowDialog() == true;
        }
        finally
        {
            IsShowing = false;
        }
    }

    /// <summary>
    /// Shows the controls that belong to a state and hides the rest.
    /// </summary>
    /// <param name="state">
    /// The state to show.
    /// </param>
    private void SetState(UpdateDialogState state)
    {
        _state = state;
        var layout = UpdateDialogModel.LayoutFor(state, _hasInstaller);

        InfoText.Visibility = Visible(layout.ShowInfo);
        ProgressPanel.Visibility = Visible(layout.ShowProgress);
        ErrorText.Visibility = Visible(layout.ShowError);
        UpdateButton.Visibility = Visible(layout.ShowUpdate);
        UpdateButton.Content = state == UpdateDialogState.Failed ? Strings.UpdateRetry : Strings.UpdateNow;
        SkipButton.Visibility = Visible(layout.ShowSkip);
        LaterButton.Visibility = Visible(layout.ShowLater);
        LaterButton.Content = state == UpdateDialogState.Failed ? Strings.UpdateClose : Strings.UpdateRemindLater;
        ViewReleaseButton.Visibility = Visible(layout.ShowViewRelease);
        CancelDownloadButton.Visibility = Visible(layout.ShowCancel);

        if (state == UpdateDialogState.Downloading)
        {
            DownloadProgressBar.Value = 0;
            ProgressText.Text = string.Format(CultureInfo.CurrentCulture, Strings.UpdateDownloading, _update.Version);
        }
    }

    /// <summary>
    /// Turns a flag into the visibility of a control.
    /// </summary>
    /// <param name="visible">
    /// Whether the control is shown.
    /// </param>
    /// <returns>
    /// <see cref="Visibility.Visible"/> or <see cref="Visibility.Collapsed"/>.
    /// </returns>
    private static Visibility Visible(bool visible)
    {
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Switches to the failed state and shows what went wrong.
    /// </summary>
    /// <param name="message">
    /// The reason, in the language of the interface.
    /// </param>
    private void ShowFailure(string message)
    {
        ErrorText.Text = message;
        SetState(UpdateDialogState.Failed);
    }

    /// <summary>
    /// Asks for the latest progress to be shown, from whichever thread the controller reports it on.
    /// </summary>
    /// <param name="sender">
    /// The controller.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnProgressChanged(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _progressPending, 1) == 0)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, ShowLatestProgress);
        }
    }

    /// <summary>
    /// Shows the latest progress of the controller; a report that comes after the download ended is dropped.
    /// </summary>
    private void ShowLatestProgress()
    {
        Interlocked.Exchange(ref _progressPending, 0);
        if (_closed || _state != UpdateDialogState.Downloading || _updates.Progress is not { } progress)
        {
            return;
        }

        DownloadProgressBar.Value = progress.Percentage ?? 0;
        ProgressText.Text = UpdateDialogModel.FormatProgress(progress);
    }

    /// <summary>
    /// Downloads and starts the installer. When it has started the application ends, so the dialog only has to deal with
    /// what happens when it did not.
    /// </summary>
    /// <param name="sender">
    /// The button.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        // InstallAsync returns without doing anything while a check or another installation is running, or after a refusal
        // of the installer; started in such a state, its return could not be told from an installer that has started.
        if (!_updates.CanInstall)
        {
            ShowFailure(_updates.LastFailure is { Stage: not UpdateFailureStage.Check } refused
                ? UpdateDialogModel.FailureText(refused.Error)
                : Strings.UpdateBusy);
            return;
        }

        SetState(UpdateDialogState.Downloading);
        using var cancellation = new CancellationTokenSource();
        _downloadCancellation = cancellation;

        try
        {
            await _updates.InstallAsync(cancellation.Token);
            if (_closed)
            {
                return;
            }

            switch (UpdateDialogModel.Classify(_updates.LastFailure, _updates.State))
            {
                case UpdateInstallOutcome.Failed:
                    ShowFailure(UpdateDialogModel.FailureText(_updates.LastFailure!.Error));
                    break;
                case UpdateInstallOutcome.NotStarted:
                    // The file is kept, so trying again does not download it a second time.
                    ShowFailure(UpdateDialogModel.FailureText(null));
                    break;
                default:
                    DialogResult = true;
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed)
            {
                SetState(UpdateDialogState.Prompt);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The controller reports failures through its state, so this is something unforeseen; as this is an async void
            // handler, letting it go would end the application instead of leaving the dialog on its failed state.
            if (!_closed)
            {
                ShowFailure(UpdateDialogModel.FailureText(ex));
            }
        }
        finally
        {
            _downloadCancellation = null;
        }
    }

    /// <summary>
    /// Stops the download that is running and goes back to the question.
    /// </summary>
    /// <param name="sender">
    /// The button.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnCancelDownloadClick(object sender, RoutedEventArgs e)
    {
        _downloadCancellation?.Cancel();
    }

    /// <summary>
    /// Remembers that this version is not wanted, so that only a check by hand offers it again, and closes.
    /// </summary>
    /// <param name="sender">
    /// The button.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private async void OnSkipClick(object sender, RoutedEventArgs e)
    {
        var skipped = false;
        try
        {
            skipped = await _updates.SkipVersionAsync();
        }
        catch (OperationCanceledException)
        {
            // Treated like a choice that could not be recorded, below.
        }

        if (_closed)
        {
            return;
        }

        if (skipped)
        {
            DialogResult = false;
        }
        else
        {
            // Closing as if it had worked would offer the version again later without a word.
            ShowFailure(Strings.UpdateSkipFailed);
        }
    }

    /// <summary>
    /// Opens the page of the release in the browser, for a download by hand.
    /// </summary>
    /// <param name="sender">
    /// The button.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnViewReleaseClick(object sender, RoutedEventArgs e)
    {
        var address = _update.Release.HtmlUrl is { Length: > 0 } url ? url : AboutInfo.ProjectUri.AbsoluteUri + "/releases/latest";
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No browser is registered; there is nothing else the button can do.
        }
    }

    /// <summary>
    /// Fades the content in and brings the dialog to the front: it has no taskbar button, and when it is opened from the
    /// tray without an owner another window could otherwise cover it.
    /// </summary>
    /// <param name="sender">
    /// The window.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Motion.FadeIn((UIElement)Content);
        Topmost = true;
        Topmost = false;
        _ = Activate();
    }

    /// <summary>
    /// Stops a download that is still running and lets go of the controller.
    /// </summary>
    /// <param name="sender">
    /// The window.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _updates.ProgressChanged -= OnProgressChanged;
        _downloadCancellation?.Cancel();
    }
}
