using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using DeskBuddy.App.Windows;
using GitHubReleaseUpdater;
using GitHubReleaseUpdater.AutoUpdate;
using Microsoft.Extensions.Logging.Abstractions;
using static DeskBuddy.App.Tests.UpdateTestSupport;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="UpdateDialog"/> with a real window that is built but never shown: they catch a text key or a named
/// control that no longer exists, which the compiler does not.
/// </summary>
public sealed class UpdateDialogTests : IDisposable
{
    /// <summary>
    /// Temporary data directory, unique per test instance.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyDialog_{Guid.NewGuid():N}");

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
    /// A release with an installer is offered with its version, its notes and the button that updates.
    /// </summary>
    [Fact]
    public async Task Constructor_ReleaseWithInstaller_OffersTheUpdate()
    {
        // Arrange
        var (controller, update) = await FindUpdateAsync("DeskBuddy-Setup-0.2.0.exe");

        // Act and assert
        StaThread.Run(() =>
        {
            var dialog = new UpdateDialog(controller, update);

            Text(dialog, "HeaderText").Should().Contain("0.2.0");
            Text(dialog, "NotesText").Should().Be("Notes");
            Visibility(dialog, "NotesPanel").Should().Be(System.Windows.Visibility.Visible);
            Visibility(dialog, "UpdateButton").Should().Be(System.Windows.Visibility.Visible);
            Visibility(dialog, "SkipButton").Should().Be(System.Windows.Visibility.Visible);
            Visibility(dialog, "ProgressPanel").Should().Be(System.Windows.Visibility.Collapsed);
            Visibility(dialog, "ErrorText").Should().Be(System.Windows.Visibility.Collapsed);
            Visibility(dialog, "ViewReleaseButton").Should().Be(System.Windows.Visibility.Collapsed);
        });
    }

    /// <summary>
    /// A release without an installer cannot be installed from the dialog, which then only points to the release page.
    /// </summary>
    [Fact]
    public async Task Constructor_ReleaseWithoutInstaller_OnlyPointsToTheReleasePage()
    {
        // Arrange
        var (controller, update) = await FindUpdateAsync("DeskBuddy-0.2.0.zip");

        // Act and assert
        StaThread.Run(() =>
        {
            var dialog = new UpdateDialog(controller, update);

            Visibility(dialog, "UpdateButton").Should().Be(System.Windows.Visibility.Collapsed);
            Visibility(dialog, "ViewReleaseButton").Should().Be(System.Windows.Visibility.Visible);
        });
    }

    /// <summary>
    /// Nothing is shown when no version was found, and nothing stays marked as showing.
    /// </summary>
    [Fact]
    public void ShowFor_NothingFound_ShowsNothing()
    {
        // Arrange
        var controller = AppUpdates.Create(
            "0.1.0",
            _directory,
            Path.Combine(_directory, "download"),
            () => true,
            new SilentPresenter(),
            NullLogger.Instance,
            new HttpClient(_handler, disposeHandler: false))!;

        // Act
        var started = false;
        StaThread.Run(() => started = UpdateDialog.ShowFor(controller, null));

        // Assert
        started.Should().BeFalse();
        UpdateDialog.IsShowing.Should().BeFalse();
    }

    /// <summary>
    /// Finds a newer release on the stub, which is the only way to get hold of an update to offer.
    /// </summary>
    /// <param name="assetName">
    /// The file name of the one asset of the release.
    /// </param>
    /// <returns>
    /// The controller that found it and the update.
    /// </returns>
    private async Task<(UpdateController Controller, AvailableUpdate Update)> FindUpdateAsync(string assetName)
    {
        _handler.Respond(HttpStatusCode.OK, ReleaseJson("v0.2.0", assetName, digest: InstallerDigest));
        var controller = AppUpdates.Create(
            "0.1.0",
            _directory,
            Path.Combine(_directory, "download"),
            () => true,
            new SilentPresenter(),
            NullLogger.Instance,
            new HttpClient(_handler, disposeHandler: false))!;
        await controller.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        return (controller, controller.Available!);
    }

    /// <summary>
    /// Reads the text of a named text block of the dialog.
    /// </summary>
    /// <param name="dialog">
    /// The dialog.
    /// </param>
    /// <param name="name">
    /// The name of the text block in the XAML.
    /// </param>
    /// <returns>
    /// Its text.
    /// </returns>
    private static string Text(Window dialog, string name)
    {
        return ((System.Windows.Controls.TextBlock)dialog.FindName(name)).Text;
    }

    /// <summary>
    /// Reads the visibility of a named element of the dialog.
    /// </summary>
    /// <param name="dialog">
    /// The dialog.
    /// </param>
    /// <param name="name">
    /// The name of the element in the XAML.
    /// </param>
    /// <returns>
    /// Its visibility.
    /// </returns>
    private static Visibility Visibility(Window dialog, string name)
    {
        return ((UIElement)dialog.FindName(name)).Visibility;
    }

    /// <summary>
    /// Says nothing and agrees to nothing, which is what every member of the interface does by default.
    /// </summary>
    private sealed class SilentPresenter : IUpdatePresenter
    {
    }
}
