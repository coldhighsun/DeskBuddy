using System.Windows;
using DeskBuddy.App.Windows;
using H.NotifyIcon.Core;

namespace DeskBuddy.App;

/// <summary>
/// The part of the tray icon service that shows balloon notices and starts the update dialog from them.
/// </summary>
public sealed partial class TrayIconService
{
    /// <summary>
    /// Shows a balloon notice from the tray icon.
    /// </summary>
    /// <param name="title">The notice title.</param>
    /// <param name="message">The notice text.</param>
    /// <param name="installsUpdate">
    /// <see langword="true"/> if clicking the notice should offer to install the version that was found.
    /// </param>
    public void ShowInfoNotice(string title, string message, bool installsUpdate = false)
    {
        _updateNoticeShown = installsUpdate;
        _icon.ShowNotification(title, message, NotificationIcon.Info);
    }

    /// <summary>
    /// Offers to install the new version when the notice announcing it is clicked.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private void OnBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        if (_updateNoticeShown)
        {
            _updateNoticeShown = false;
            _ = RunUpdateAsync(InstallUpdateAsync);
        }
    }

    /// <summary>
    /// Opens the update dialog for the version that was found, where the user decides whether to install it. Does nothing
    /// without an update controller.
    /// </summary>
    /// <returns>A task that completes when the dialog has been closed.</returns>
    private Task InstallUpdateAsync()
    {
        if (_updates is { } updates)
        {
            _ = UpdateDialog.ShowFor(updates, null);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs an update action started from the menu or a notice.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <returns>A task that completes when the action is done.</returns>
    private Task RunUpdateAsync(Func<Task> action)
    {
        return UpdateRunner.RunAsync(action, _logger);
    }
}
