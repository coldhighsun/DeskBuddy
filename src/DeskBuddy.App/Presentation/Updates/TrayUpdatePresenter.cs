using System.Windows;
using GitHubReleaseUpdater;
using GitHubReleaseUpdater.AutoUpdate;
using GitHubReleaseUpdater.Exceptions;
using GitHubReleaseUpdater.Versioning;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Talks to the user about updates through balloon notices of the tray icon, and closes the application when the installer
/// has been started. The offer itself, the download and its failures are shown by the update dialog.
/// </summary>
/// <param name="tray">
/// Gives the tray icon, which is made after the controller that uses this presenter; <see langword="null"/> while it does
/// not exist.
/// </param>
/// <param name="exit">Closes the application so the installer can replace its files.</param>
/// <param name="logger">Reports why a check or an installation failed.</param>
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
    /// <remarks>
    /// The user has already decided by pressing the button of the update dialog, which is the only way to start an
    /// installation, so nothing is asked again.
    /// </remarks>
    public ValueTask<bool> ConfirmInstallAsync(AvailableUpdate update, SemanticVersion currentVersion)
    {
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Only logged: the update dialog that started the installation shows the reason itself, with a button to try again.
    /// </remarks>
    public void ReportInstallFailed(AvailableUpdate update, Exception error)
    {
        logger.LogWarning(error, "Version {Version} could not be installed.", update.Version);
    }

    /// <inheritdoc />
    public void InstallerStarted(AvailableUpdate update)
    {
        exit();
    }
}
