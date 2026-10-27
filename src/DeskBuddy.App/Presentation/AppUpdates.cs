using System.IO;
using System.Net.Http;
using GitHubReleaseUpdater;
using GitHubReleaseUpdater.Assets;
using GitHubReleaseUpdater.AutoUpdate;
using GitHubReleaseUpdater.Installation;
using GitHubReleaseUpdater.LastCheck;
using GitHubReleaseUpdater.Versioning;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Sets up the automatic update of the application: looking for releases of the project on GitHub and installing one
/// by starting the Inno Setup installer that is attached to it.
/// </summary>
internal static class AppUpdates
{
    /// <summary>
    /// Owner of the repository that publishes the releases.
    /// </summary>
    public const string RepositoryOwner = "coldhighsun";

    /// <summary>
    /// Name of the repository that publishes the releases.
    /// </summary>
    public const string RepositoryName = "DeskBuddy";

    /// <summary>
    /// File name of the data file that remembers when the last check was made.
    /// </summary>
    public const string StateFileName = "update-state.json";

    /// <summary>
    /// Pause kept between two automatic checks, so restarting the application does not query GitHub every time.
    /// </summary>
    public static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromHours(6);

    /// <summary>
    /// Switch that tells the installer to start the application again once it is done; the installer does not do that after
    /// a silent run unless asked.
    /// </summary>
    internal const string RelaunchArgument = "/RELAUNCH=1";

    /// <summary>
    /// Picks the installer among the release assets: an <c>.exe</c> with <c>Setup</c> in its name. The default selection
    /// looks for an operating system and architecture in the name, which the installer's name does not have.
    /// </summary>
    private static readonly PatternAssetSelector InstallerSelector = new("*Setup*.exe");

    /// <summary>
    /// Tells whether a version is the one MinVer gives a build that has no git tag to start from (<c>0.0.0-alpha.0.N</c>),
    /// which is how a build from a development checkout is told from a release.
    /// </summary>
    /// <param name="version">
    /// The running version.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a development build, which is older than every release and so would always be offered
    /// one.
    /// </returns>
    public static bool IsDevelopmentBuild(SemanticVersion version)
    {
        return version is { Major: 0, Minor: 0, Patch: 0, IsPrerelease: true };
    }

    /// <summary>
    /// Creates the controller that keeps the application up to date.
    /// </summary>
    /// <param name="currentVersion">
    /// The running version as text, for example <c>0.2.0</c> or <c>0.2.0-rc.1</c>.
    /// </param>
    /// <param name="dataDirectory">
    /// The directory that holds the data files, where the time of the last check is kept.
    /// </param>
    /// <param name="downloadDirectory">
    /// The directory the installer is downloaded to; earlier downloads in it are deleted, so it must be used for nothing
    /// else.
    /// </param>
    /// <param name="isAutoCheckEnabled">
    /// Says whether the user wants automatic checks; asked before each one. A development build never checks by itself,
    /// whatever this says, but can still be asked to by hand.
    /// </param>
    /// <param name="presenter">
    /// Talks to the user.
    /// </param>
    /// <param name="logger">
    /// Reports what went wrong inside the controller.
    /// </param>
    /// <param name="httpClient">
    /// The client to talk to GitHub with; <see langword="null"/> uses the library's own. Tests pass one with a stubbed
    /// handler.
    /// </param>
    /// <param name="time">
    /// The clock the waits and the pause between checks are measured on; <see langword="null"/> uses the system clock.
    /// </param>
    /// <param name="installerLauncher">
    /// Starts the downloaded installer; <see langword="null"/> starts it as a process. Tests pass one that only records.
    /// </param>
    /// <returns>
    /// The controller, or <see langword="null"/> if the running version is not a valid version number (for example a build
    /// without version information), in which case there is nothing to compare releases with.
    /// </returns>
    public static UpdateController? Create(
        string currentVersion,
        string dataDirectory,
        string downloadDirectory,
        Func<bool> isAutoCheckEnabled,
        GitHubReleaseUpdater.AutoUpdate.IUpdatePresenter presenter,
        ILogger logger,
        HttpClient? httpClient = null,
        TimeProvider? time = null,
        IInstallerLauncher? installerLauncher = null)
    {
        if (!SemanticVersion.TryParse(currentVersion, out var version))
        {
            logger.LogWarning("Updates are not checked because {Version} is not a version number.", currentVersion);
            return null;
        }

        var updater = new ReleaseUpdater(new UpdaterOptions
        {
            Owner = RepositoryOwner,
            Repo = RepositoryName,
            CurrentVersion = version,
            IncludePrerelease = false,
            AssetSelector = InstallerSelector,
            LastCheckStore = new JsonFileLastCheckStore(Path.Combine(dataDirectory, StateFileName)),
            MinimumCheckInterval = MinimumCheckInterval,
            HttpClient = httpClient,
            TimeProvider = time,
            InstallerLauncher = installerLauncher,
        });
        return new UpdateController(updater, presenter, new UpdateControllerOptions
        {
            IsAutoCheckEnabled = () => isAutoCheckEnabled() && !IsDevelopmentBuild(version),
            DownloadDirectory = downloadDirectory,
            CleanDownloadDirectory = true,
            InstallerLaunchOptions = InstallerLaunchOptions.InnoSetupSilent.WithArguments(RelaunchArgument),

            // An installer is only started if its checksum was verified against what the release publishes (a sidecar file,
            // a sums file or the digest GitHub reports); a release without one is refused instead of run unchecked.
            RequireVerifiedDownload = true,
            TimeProvider = time,
            OnError = error => logger.LogWarning(error, "The update flow met a problem."),
        });
    }
}
