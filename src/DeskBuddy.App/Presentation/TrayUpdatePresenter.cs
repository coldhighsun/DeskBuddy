using System.Windows;
using GitHubReleaseUpdater;
using GitHubReleaseUpdater.AutoUpdate;
using GitHubReleaseUpdater.Exceptions;
using GitHubReleaseUpdater.Versioning;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Talks to the user about updates through balloon notices of the tray icon and a message box, and closes the
/// application when the installer has been started.
/// </summary>
/// <param name="tray">
/// Gives the tray icon, which is made after the controller that uses this presenter; <see langword="null"/> while it does
/// not exist.
/// </param>
/// <param name="exit">
/// Closes the application so the installer can replace its files.
/// </param>
/// <param name="logger">
/// Reports why a check or an installation failed.
/// </param>
internal sealed class TrayUpdatePresenter(Func<TrayIconService?> tray, Action exit, ILogger logger) : IUpdatePresenter
{
    /// <inheritdoc />
    public void Announce(AvailableUpdate update)
    {
        tray()?.ShowInfoNotice(
            string.Format(CultureInfo.CurrentCulture, Strings.UpdateAvailable, update.Version),
            Strings.UpdateAvailableBody,
            installsUpdate: true);
    }

    /// <inheritdoc />
    public void ReportUpToDate()
    {
        tray()?.ShowInfoNotice(Strings.AppName, Strings.UpdateUpToDate);
    }

    /// <inheritdoc />
    public void ReportCheckFailed(Exception? error)
    {
        logger.LogWarning(error, "The check for updates failed.");
        if (error is AssetNotFoundException)
        {
            // Nothing went wrong on the user's side: a version exists, there is just nothing to download from it.
            tray()?.ShowInfoNotice(Strings.AppName, Strings.UpdateNoInstallerNotice);
            return;
        }

        tray()?.ShowErrorNotice(Strings.AppName, Strings.UpdateCheckFailed);
    }

    /// <inheritdoc />
    public ValueTask<bool> ConfirmInstallAsync(AvailableUpdate update, SemanticVersion currentVersion)
    {
        var question = string.Format(CultureInfo.CurrentCulture, Strings.UpdateConfirm, update.Version, currentVersion);
        var answer = MessageBox.Show(question, Strings.UpdateConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return ValueTask.FromResult(answer == MessageBoxResult.Yes);
    }

    /// <inheritdoc />
    public void ReportDownloading(AvailableUpdate update)
    {
        tray()?.ShowInfoNotice(
            Strings.AppName,
            string.Format(CultureInfo.CurrentCulture, Strings.UpdateDownloading, update.Version));
    }

    /// <inheritdoc />
    public void ReportInstallFailed(AvailableUpdate update, Exception error)
    {
        logger.LogWarning(error, "Version {Version} could not be installed.", update.Version);
        tray()?.ShowErrorNotice(Strings.AppName, string.Format(CultureInfo.CurrentCulture, Strings.UpdateInstallFailed, UpdateErrorText.For(error)));
    }

    /// <inheritdoc />
    public void InstallerStarted(AvailableUpdate update)
    {
        exit();
    }
}
