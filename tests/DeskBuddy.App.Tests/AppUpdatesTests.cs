using DeskBuddy.App.Resources;
using System.IO;
using System.Net;
using System.Net.Http;
using GitHubReleaseUpdater;
using GitHubReleaseUpdater.AutoUpdate;
using GitHubReleaseUpdater.Installation;
using Microsoft.Extensions.Logging.Abstractions;
using GitHubReleaseUpdater.Versioning;
using Microsoft.Extensions.Time.Testing;
using static DeskBuddy.App.Tests.UpdateTestSupport;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="AppUpdates"/>, with a stub in place of GitHub and a real temporary directory per test.
/// </summary>
public sealed class AppUpdatesTests : IDisposable
{
    /// <summary>
    /// Temporary data directory, unique per test instance.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyUpdates_{Guid.NewGuid():N}");

    /// <summary>
    /// The handler that answers for GitHub.
    /// </summary>
    private readonly StubHandler _handler = new();

    /// <summary>
    /// The fake user.
    /// </summary>
    private readonly RecordingPresenter _presenter = new();

    /// <summary>
    /// How many times the switch for automatic checks was asked.
    /// </summary>
    private int _rounds;

    /// <summary>
    /// The launcher that records instead of running anything.
    /// </summary>
    private readonly RecordingLauncher _launcher = new();

    /// <summary>
    /// The clock the pause between checks is measured on.
    /// </summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    /// <inheritdoc />
    public void Dispose()
    {
        _handler.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// A version that is not a number leaves nothing to compare releases with, so there is no controller.
    /// </summary>
    [Theory]
    [InlineData("?")]
    [InlineData("")]
    [InlineData("not a version")]
    public void Create_VersionIsNotANumber_ReturnsNull(string version)
    {
        // Act
        var controller = AppUpdates.Create(version, _directory, _directory, () => true, _presenter, NullLogger.Instance);

        // Assert
        controller.Should().BeNull();
    }

    /// <summary>
    /// An automatic check that finds a newer release with a setup file announces it.
    /// </summary>
    [Fact]
    public async Task CheckAsync_NewerReleaseWithSetup_IsAnnounced()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe"));
        var controller = CreateController("0.1.0");

        // Act
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        // Assert
        controller.State.Should().Be(UpdateState.Available);
        controller.HasInstaller.Should().BeTrue();
        controller.CanInstall.Should().BeTrue();
        controller.IsBusy.Should().BeFalse();
        controller.Available!.Version.ToString().Should().Be("0.2.0");
        controller.Available.SelectedAsset!.Name.Should().Be("DeskBuddy-Setup-0.2.0.exe");
        _presenter.Announced.Should().ContainSingle();
    }

    /// <summary>
    /// A release with the running version is not an update.
    /// </summary>
    [Fact]
    public async Task CheckAsync_SameVersion_IsUpToDate()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.1.0", "DeskBuddy-Setup-0.1.0.exe"));
        var controller = CreateController("0.1.0");

        // Act
        await controller.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        // Assert
        controller.State.Should().Be(UpdateState.UpToDate);
        _presenter.UpToDateReports.Should().Be(1);
    }

    /// <summary>
    /// Only a file with <c>Setup</c> in its name and the <c>.exe</c> extension is taken for the installer.
    /// </summary>
    [Fact]
    public async Task CheckAsync_NewerReleaseWithoutSetup_HasNoInstaller()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-0.2.0.zip"));
        var controller = CreateController("0.1.0");

        // Act
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        // Assert
        controller.HasInstaller.Should().BeFalse();
        controller.CanInstall.Should().BeFalse();
        controller.Available!.Version.ToString().Should().Be("0.2.0");
        _presenter.Announced.Should().BeEmpty();
    }

    /// <summary>
    /// A failing GitHub is reported to a manual check.
    /// </summary>
    [Fact]
    public async Task CheckAsync_ServerError_IsReportedToManualCheck()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.InternalServerError, "{}");
        var controller = CreateController("0.1.0");

        // Act
        await controller.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        // Assert
        controller.State.Should().Be(UpdateState.Failed);
        _presenter.CheckFailures.Should().Be(1);
    }

    /// <summary>
    /// An automatic check right after another is held back, a manual one is not.
    /// </summary>
    [Fact]
    public async Task CheckAsync_AutomaticRightAfterAnother_IsThrottledButManualIsNot()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.1.0", "DeskBuddy-Setup-0.1.0.exe"));
        var controller = CreateController("0.1.0");
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        var afterFirst = _handler.RequestCount;

        // Act
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        var afterAutomatic = _handler.RequestCount;
        await controller.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        // Assert
        afterAutomatic.Should().Be(afterFirst);
        _handler.RequestCount.Should().BeGreaterThan(afterAutomatic);
    }

    /// <summary>
    /// The pause is over after <see cref="AppUpdates.MinimumCheckInterval"/>.
    /// </summary>
    [Fact]
    public async Task CheckAsync_AutomaticAfterTheInterval_AsksAgain()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.1.0", "DeskBuddy-Setup-0.1.0.exe"));
        var controller = CreateController("0.1.0");
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        var afterFirst = _handler.RequestCount;
        _time.Advance(AppUpdates.MinimumCheckInterval + TimeSpan.FromMinutes(1));

        // Act
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        // Assert
        _handler.RequestCount.Should().BeGreaterThan(afterFirst);
    }

    /// <summary>
    /// Only the latest full release is asked for; pre-releases are never offered.
    /// </summary>
    [Fact]
    public async Task CheckAsync_Always_AsksForTheLatestFullRelease()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.1.0", "DeskBuddy-Setup-0.1.0.exe"));
        var controller = CreateController("0.1.0");

        // Act
        await controller.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequestPath.Should().Be("/repos/coldhighsun/DeskBuddy/releases/latest");
    }

    /// <summary>
    /// The time of the check is kept in the data directory, so the pause survives a restart.
    /// </summary>
    [Fact]
    public async Task CheckAsync_Always_RemembersTheCheckInTheDataDirectory()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.1.0", "DeskBuddy-Setup-0.1.0.exe"));
        var controller = CreateController("0.1.0");

        // Act
        await controller.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(Path.Combine(_directory, AppUpdates.StateFileName)).Should().BeTrue();
    }

    /// <summary>
    /// Installing before anything was found does nothing and asks nothing.
    /// </summary>
    [Fact]
    public async Task InstallAsync_NothingFoundYet_AsksNothing()
    {
        // Arrange
        var controller = CreateController("0.1.0");

        // Act
        await controller.InstallAsync(TestContext.Current.CancellationToken);

        // Assert
        _presenter.Confirmations.Should().Be(0);
        _handler.RequestCount.Should().Be(0);
    }

    /// <summary>
    /// A verified installer is started with the silent Inno Setup switches, from the download directory, and the
    /// application is told to make way for it.
    /// </summary>
    [Fact]
    public async Task InstallAsync_VerifiedInstaller_IsStartedSilently()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe", digest: InstallerDigest));
        _presenter.ConfirmAnswer = true;
        var controller = CreateController("0.1.0");
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        // Act
        await controller.InstallAsync(TestContext.Current.CancellationToken);

        // Assert
        var launch = _launcher.Launched.Should().ContainSingle().Subject;
        launch.Path.Should().StartWith(Path.Combine(_directory, "download"));
        launch.Path.Should().EndWith("DeskBuddy-Setup-0.2.0.exe");
        File.ReadAllBytes(launch.Path).Should().Equal(InstallerBytes);
        launch.Options.Arguments.Should().Be("/SILENT /SP- /NORESTART /RELAUNCH=1");
        _presenter.InstallerStartedCount.Should().Be(1);
        _presenter.InstallFailures.Should().BeEmpty();
    }

    /// <summary>
    /// An installer that the release publishes no checksum for is refused, not run unchecked.
    /// </summary>
    [Fact]
    public async Task InstallAsync_NoChecksumPublished_IsRefusedAndNotStarted()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe"));
        _presenter.ConfirmAnswer = true;
        var controller = CreateController("0.1.0");
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        // Act
        await controller.InstallAsync(TestContext.Current.CancellationToken);

        // Assert
        _launcher.Launched.Should().BeEmpty();
        _presenter.InstallerStartedCount.Should().Be(0);
        var error = _presenter.InstallFailures.Should().ContainSingle().Subject;
        UpdateErrorText.For(error).Should().Be(Strings.UpdateErrorUnverified);
    }

    /// <summary>
    /// Nothing is downloaded when the user declines.
    /// </summary>
    [Fact]
    public async Task InstallAsync_Declined_DownloadsNothing()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe", digest: InstallerDigest));
        var controller = CreateController("0.1.0");
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        var requests = _handler.RequestCount;

        // Act
        await controller.InstallAsync(TestContext.Current.CancellationToken);

        // Assert
        _handler.RequestCount.Should().Be(requests);
        _launcher.Launched.Should().BeEmpty();
        _presenter.Confirmations.Should().Be(1);
    }

    /// <summary>
    /// Installers left by earlier updates are deleted once the application is up to date.
    /// </summary>
    [Fact]
    public async Task CheckAsync_UpToDate_DeletesEarlierDownloads()
    {
        // Arrange
        var download = Path.Combine(_directory, "download");
        Directory.CreateDirectory(download);
        var leftover = Path.Combine(download, "DeskBuddy-Setup-0.1.0.exe");
        File.WriteAllBytes(leftover, InstallerBytes);
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.1.0", "DeskBuddy-Setup-0.1.0.exe"));
        var controller = CreateController("0.1.0");

        // Act
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(leftover).Should().BeFalse();
    }

    /// <summary>
    /// The background loop checks by itself when the user wants it to.
    /// </summary>
    [Fact]
    public async Task RunAsync_AutoCheckEnabled_ChecksOnItsOwn()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe", digest: InstallerDigest));
        var controller = CreateController("0.1.0", CountRounds(enabled: true));

        // Act
        await RunRoundsAsync(controller, 2);

        // Assert
        _handler.RequestCount.Should().BeGreaterThan(0);
        _presenter.Announced.Should().ContainSingle();
    }

    /// <summary>
    /// Turning off the setting for automatic checks stops the loop from asking GitHub.
    /// </summary>
    [Fact]
    public async Task RunAsync_AutoCheckDisabled_NeverAsksGitHub()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe", digest: InstallerDigest));
        var controller = CreateController("0.1.0", CountRounds(enabled: false));

        // Act
        await RunRoundsAsync(controller, 2);

        // Assert
        _handler.RequestCount.Should().Be(0);
        _presenter.Announced.Should().BeEmpty();
    }

    /// <summary>
    /// A development build, whose version is MinVer's default, is never offered the release on its own, but the user can
    /// still ask by hand.
    /// </summary>
    [Fact]
    public async Task RunAsync_DevelopmentBuild_ChecksOnlyWhenAskedByHand()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe", digest: InstallerDigest));
        var controller = CreateController("0.0.0-alpha.0.12", CountRounds(enabled: true));

        // Act
        await RunRoundsAsync(controller, 2);
        var automatic = _handler.RequestCount;
        await controller.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        // Assert
        automatic.Should().Be(0);
        _handler.RequestCount.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Only the version MinVer gives a build without a tag counts as a development build.
    /// </summary>
    [Theory]
    [InlineData("0.0.0-alpha.0.12", true)]
    [InlineData("0.0.0", false)]
    [InlineData("0.0.1", false)]
    [InlineData("0.1.0", false)]
    [InlineData("0.1.0-rc.1", false)]
    public void IsDevelopmentBuild_Version_IsRecognized(string version, bool expected)
    {
        AppUpdates.IsDevelopmentBuild(SemanticVersion.Parse(version)).Should().Be(expected);
    }

    /// <summary>
    /// Makes the switch for automatic checks and counts how often it is asked.
    /// </summary>
    /// <param name="enabled">
    /// What the switch answers.
    /// </param>
    /// <returns>
    /// The switch.
    /// </returns>
    private Func<bool> CountRounds(bool enabled)
    {
        return () =>
        {
            Interlocked.Increment(ref _rounds);
            return enabled;
        };
    }

    /// <summary>
    /// Runs the background loop until it has made the given number of rounds, moving the clock past the waits.
    /// </summary>
    /// <param name="controller">
    /// The controller whose loop runs.
    /// </param>
    /// <param name="rounds">
    /// How many times the loop has asked whether to check. The check of an earlier round is done by the time a later
    /// round asks.
    /// </param>
    /// <returns>
    /// A task that completes when the loop has been stopped.
    /// </returns>
    private async Task RunRoundsAsync(UpdateController controller, int rounds)
    {
        using var stop = new CancellationTokenSource();
        var loop = controller.RunAsync(stop.Token);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (Volatile.Read(ref _rounds) < rounds && DateTime.UtcNow < deadline)
        {
            _time.Advance(TimeSpan.FromHours(25));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await stop.CancelAsync();
        await loop;
        Volatile.Read(ref _rounds).Should().BeGreaterThanOrEqualTo(rounds);
    }

    /// <summary>
    /// Creates the controller under test.
    /// </summary>
    /// <param name="currentVersion">
    /// The running version.
    /// </param>
    /// <param name="isAutoCheckEnabled">
    /// Says whether the user wants automatic checks; <see langword="null"/> means yes.
    /// </param>
    /// <returns>
    /// The controller.
    /// </returns>
    private UpdateController CreateController(string currentVersion, Func<bool>? isAutoCheckEnabled = null)
    {
        var client = new HttpClient(_handler, disposeHandler: false);
        return AppUpdates.Create(
            currentVersion,
            _directory,
            Path.Combine(_directory, "download"),
            isAutoCheckEnabled ?? (() => true),
            _presenter,
            NullLogger.Instance,
            client,
            _time,
            _launcher)!;
    }

    /// <summary>
    /// Records what the controller tells the user.
    /// </summary>
    private sealed class RecordingPresenter : GitHubReleaseUpdater.AutoUpdate.IUpdatePresenter
    {
        /// <summary>
        /// Gets the versions that were announced.
        /// </summary>
        public List<AvailableUpdate> Announced { get; } = [];

        /// <summary>
        /// Gets how many times the user was told the application is up to date.
        /// </summary>
        public int UpToDateReports { get; private set; }

        /// <summary>
        /// Gets how many times the user was told a check failed.
        /// </summary>
        public int CheckFailures { get; private set; }

        /// <summary>
        /// Gets how many times the user was asked whether to install.
        /// </summary>
        public int Confirmations { get; private set; }

        /// <summary>
        /// Gets or sets what the user answers when asked whether to install.
        /// </summary>
        public bool ConfirmAnswer { get; set; }

        /// <summary>
        /// Gets the reasons given for installations that failed.
        /// </summary>
        public List<Exception> InstallFailures { get; } = [];

        /// <summary>
        /// Gets how many times the application was told the installer has started.
        /// </summary>
        public int InstallerStartedCount { get; private set; }

        /// <inheritdoc />
        public void Announce(AvailableUpdate update)
        {
            Announced.Add(update);
        }

        /// <inheritdoc />
        public void ReportUpToDate()
        {
            UpToDateReports++;
        }

        /// <inheritdoc />
        public void ReportCheckFailed(Exception? error)
        {
            CheckFailures++;
        }

        /// <inheritdoc />
        public ValueTask<bool> ConfirmInstallAsync(AvailableUpdate update, GitHubReleaseUpdater.Versioning.SemanticVersion currentVersion)
        {
            Confirmations++;
            return ValueTask.FromResult(ConfirmAnswer);
        }

        /// <inheritdoc />
        public void ReportInstallFailed(AvailableUpdate update, Exception error)
        {
            InstallFailures.Add(error);
        }

        /// <inheritdoc />
        public void InstallerStarted(AvailableUpdate update)
        {
            InstallerStartedCount++;
        }
    }

    /// <summary>
    /// Remembers which installers it was asked to start instead of starting them.
    /// </summary>
    private sealed class RecordingLauncher : IInstallerLauncher
    {
        /// <summary>
        /// Gets the installers that were started.
        /// </summary>
        public List<(string Path, InstallerLaunchOptions Options)> Launched { get; } = [];

        /// <inheritdoc />
        public InstallerLaunchResult Launch(string installerPath, InstallerLaunchOptions options)
        {
            Launched.Add((installerPath, options));
            return new InstallerLaunchResult(installerPath, null);
        }
    }
}
