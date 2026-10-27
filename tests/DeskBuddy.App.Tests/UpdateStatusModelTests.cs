using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Resources;
using GitHubReleaseUpdater;
using GitHubReleaseUpdater.AutoUpdate;
using GitHubReleaseUpdater.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static DeskBuddy.App.Tests.UpdateTestSupport;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="UpdateStatusModel"/>.
/// </summary>
public sealed class UpdateStatusModelTests : IDisposable
{
    /// <summary>
    /// Temporary data directory, unique per test instance.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyStatus_{Guid.NewGuid():N}");

    /// <summary>
    /// The handler that answers for GitHub.
    /// </summary>
    private readonly StubHandler _handler = new();

    /// <summary>
    /// The clock the controller measures its pauses on.
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
    /// Before any check there is nothing to say.
    /// </summary>
    [Fact]
    public void For_NothingChecked_IsEmpty()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var text = UpdateStatusModel.For(controller);

        // Assert
        text.Should().BeEmpty();
    }

    /// <summary>
    /// A newer version with an installer is announced with its version.
    /// </summary>
    [Fact]
    public async Task For_NewerReleaseWithSetup_SaysUpdateIsAvailable()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe"));
        var controller = CreateController();

        // Act
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        // Assert
        UpdateStatusModel.For(controller).Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.UpdateAvailable, "0.2.0"));
    }

    /// <summary>
    /// A release without an installer says so instead of offering it.
    /// </summary>
    [Fact]
    public async Task For_ReleaseWithoutInstaller_SaysThereIsNoInstaller()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-0.2.0.zip"));
        var controller = CreateController();

        // Act
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        // Assert
        UpdateStatusModel.For(controller).Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.UpdateNoInstaller, "0.2.0"));
    }

    /// <summary>
    /// A release with the running version says the application is up to date.
    /// </summary>
    [Fact]
    public async Task For_SameVersion_SaysUpToDate()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.1.0", "DeskBuddy-Setup-0.1.0.exe"));
        var controller = CreateController();

        // Act
        await controller.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        // Assert
        UpdateStatusModel.For(controller).Should().Be(Strings.UpdateUpToDate);
    }

    /// <summary>
    /// A check that failed says so.
    /// </summary>
    [Fact]
    public async Task For_FailedCheck_SaysCheckFailed()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.InternalServerError, "{}");
        var controller = CreateController();

        // Act
        await controller.CheckAsync(manual: true, TestContext.Current.CancellationToken);

        // Assert
        UpdateStatusModel.For(controller).Should().Be(Strings.UpdateCheckFailed);
    }

    /// <summary>
    /// Creates a controller for the running version 0.1.0 that talks to the stub.
    /// </summary>
    /// <returns>
    /// The controller.
    /// </returns>
    private UpdateController CreateController()
    {
        return AppUpdates.Create(
            "0.1.0",
            _directory,
            Path.Combine(_directory, "download"),
            () => true,
            new SilentPresenter(),
            NullLogger.Instance,
            new HttpClient(_handler, disposeHandler: false),
            _time)!;
    }

    /// <summary>
    /// A presenter that tells the user nothing and declines every installation.
    /// </summary>
    private sealed class SilentPresenter : IUpdatePresenter
    {
        /// <inheritdoc />
        public void Announce(AvailableUpdate update)
        {
        }

        /// <inheritdoc />
        public void ReportUpToDate()
        {
        }

        /// <inheritdoc />
        public void ReportCheckFailed(Exception? error)
        {
        }

        /// <inheritdoc />
        public ValueTask<bool> ConfirmInstallAsync(AvailableUpdate update, SemanticVersion currentVersion)
        {
            return ValueTask.FromResult(false);
        }

        /// <inheritdoc />
        public void ReportDownloading(AvailableUpdate update)
        {
        }

        /// <inheritdoc />
        public void ReportInstallFailed(AvailableUpdate update, Exception error)
        {
        }

        /// <inheritdoc />
        public void InstallerStarted(AvailableUpdate update)
        {
        }
    }
}
