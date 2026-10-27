using DeskBuddy.App.Resources;
using System.IO;
using GitHubReleaseUpdater.Exceptions;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="UpdateErrorText"/>.
/// </summary>
public sealed class UpdateErrorTextTests
{
    /// <summary>
    /// The messages of the update library are English; the user gets a sentence of the interface language instead.
    /// </summary>
    [Fact]
    public void For_Error_GivesLocalizedSentence()
    {
        UpdateErrorText.For(new ChecksumMismatchException("file", "a", "b")).Should().Be(Strings.UpdateErrorChecksum);
        UpdateErrorText.For(new AssetNotFoundException("none", [])).Should().Be(Strings.UpdateNoInstallerNotice);
        UpdateErrorText.For(new InstallerLaunchException("fail", "file", isUserCancelled: false)).Should().Be(Strings.UpdateErrorLaunch);
        UpdateErrorText.For(new UpdaterException("download truncated")).Should().Be(Strings.UpdateErrorUnverified);
        UpdateErrorText.For(new IOException("disk")).Should().Be(Strings.UpdateErrorGeneric);
    }
}
