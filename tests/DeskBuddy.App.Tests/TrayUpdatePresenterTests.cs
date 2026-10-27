using System.IO;
using System.Net;
using System.Net.Http;
using GitHubReleaseUpdater;
using GitHubReleaseUpdater.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using static DeskBuddy.App.Tests.UpdateTestSupport;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="TrayUpdatePresenter"/>, without a tray icon: what it needs of one is optional.
/// </summary>
public sealed class TrayUpdatePresenterTests : IDisposable
{
    /// <summary>
    /// Temporary data directory, unique per test instance.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyPresenter_{Guid.NewGuid():N}");

    /// <summary>
    /// The handler that answers for GitHub.
    /// </summary>
    private readonly StubHandler _handler = new();

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
    /// The application is asked to close as soon as the installer has been started, and nothing else closes it.
    /// </summary>
    [Fact]
    public async Task InstallerStarted_Always_ClosesTheApplicationOnce()
    {
        // Arrange
        var update = await FindUpdateAsync();
        var exits = 0;
        var presenter = new TrayUpdatePresenter(() => null, () => exits++, NullLogger.Instance);

        // Act
        presenter.Announce(update);
        presenter.ReportUpToDate();
        presenter.ReportCheckFailed(new AssetNotFoundException("none", []));
        presenter.ReportCheckFailed(null);
        presenter.ReportDownloading(update);
        presenter.ReportInstallFailed(update, new IOException("disk"));
        var exitsBefore = exits;
        presenter.InstallerStarted(update);

        // Assert
        exitsBefore.Should().Be(0);
        exits.Should().Be(1);
    }

    /// <summary>
    /// Finds the newer release on the stub, which is the only way to get hold of an update to talk about.
    /// </summary>
    /// <returns>
    /// The update.
    /// </returns>
    private async Task<AvailableUpdate> FindUpdateAsync()
    {
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", "DeskBuddy-Setup-0.2.0.exe", digest: InstallerDigest));
        var controller = AppUpdates.Create(
            "0.1.0",
            _directory,
            Path.Combine(_directory, "download"),
            () => true,
            new SilentPresenter(),
            NullLogger.Instance,
            new HttpClient(_handler, disposeHandler: false))!;
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        return controller.Available!;
    }

    /// <summary>
    /// Says nothing and agrees to nothing, which is what every member of the interface does by default.
    /// </summary>
    private sealed class SilentPresenter : GitHubReleaseUpdater.AutoUpdate.IUpdatePresenter
    {
    }
}
