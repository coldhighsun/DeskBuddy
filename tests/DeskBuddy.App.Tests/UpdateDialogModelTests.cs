using System.Globalization;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Resources;
using System.IO;
using GitHubReleaseUpdater.AutoUpdate;
using GitHubReleaseUpdater.Download;
using GitHubReleaseUpdater.Exceptions;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="UpdateDialogModel"/>, the part of the update dialog that needs no window.
/// </summary>
public sealed class UpdateDialogModelTests : IDisposable
{
    /// <summary>
    /// The interface culture before the test, put back afterwards.
    /// </summary>
    private readonly CultureInfo _original = CultureInfo.CurrentUICulture;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDialogModelTests"/> class; texts are read in English.
    /// </summary>
    public UpdateDialogModelTests()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _original;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The question shows the explanation and the three answers, and nothing of a download.
    /// </summary>
    [Fact]
    public void LayoutFor_PromptWithInstaller_ShowsTheQuestion()
    {
        // Act
        var layout = UpdateDialogModel.LayoutFor(UpdateDialogState.Prompt, hasInstaller: true);

        // Assert
        layout.Should().Be(new UpdateDialogLayout(
            ShowInfo: true,
            ShowProgress: false,
            ShowError: false,
            ShowUpdate: true,
            ShowSkip: true,
            ShowLater: true,
            ShowViewRelease: false,
            ShowCancel: false));
    }

    /// <summary>
    /// While downloading, the only thing the user can do is stop it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LayoutFor_Downloading_OnlyAllowsCancelling(bool hasInstaller)
    {
        // Act
        var layout = UpdateDialogModel.LayoutFor(UpdateDialogState.Downloading, hasInstaller);

        // Assert
        layout.Should().Be(new UpdateDialogLayout(
            ShowInfo: false,
            ShowProgress: true,
            ShowError: false,
            ShowUpdate: false,
            ShowSkip: false,
            ShowLater: false,
            ShowViewRelease: false,
            ShowCancel: true));
    }

    /// <summary>
    /// After a failure the user can try again, look at the release page or close the dialog, but not skip the version.
    /// </summary>
    [Fact]
    public void LayoutFor_Failed_OffersRetryAndTheReleasePage()
    {
        // Act
        var layout = UpdateDialogModel.LayoutFor(UpdateDialogState.Failed, hasInstaller: true);

        // Assert
        layout.Should().Be(new UpdateDialogLayout(
            ShowInfo: false,
            ShowProgress: false,
            ShowError: true,
            ShowUpdate: true,
            ShowSkip: false,
            ShowLater: true,
            ShowViewRelease: true,
            ShowCancel: false));
    }

    /// <summary>
    /// A release without an installer cannot be installed from the dialog, so only the release page is offered.
    /// </summary>
    [Fact]
    public void LayoutFor_PromptWithoutInstaller_OnlyOffersTheReleasePage()
    {
        // Act
        var layout = UpdateDialogModel.LayoutFor(UpdateDialogState.Prompt, hasInstaller: false);

        // Assert
        layout.ShowUpdate.Should().BeFalse();
        layout.ShowViewRelease.Should().BeTrue();
    }

    /// <summary>
    /// A release without notes shows nothing, whatever the empty value is.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public void TrimNotes_NoNotes_IsEmpty(string? notes)
    {
        UpdateDialogModel.TrimNotes(notes).Should().BeEmpty();
    }

    /// <summary>
    /// Notes that fit are shown as they are, without the blank lines around them.
    /// </summary>
    [Fact]
    public void TrimNotes_ShortNotes_AreTrimmedOnly()
    {
        UpdateDialogModel.TrimNotes("\n- fixed a bug\n").Should().Be("- fixed a bug");
    }

    /// <summary>
    /// Notes that are too long are cut and marked as cut.
    /// </summary>
    [Fact]
    public void TrimNotes_LongNotes_AreCutWithAnEllipsis()
    {
        // Arrange
        var notes = new string('a', UpdateDialogModel.MaxNotesLength + 100);

        // Act
        var text = UpdateDialogModel.TrimNotes(notes);

        // Assert
        text.Should().HaveLength(UpdateDialogModel.MaxNotesLength + 1);
        text.Should().EndWith("…");
    }

    /// <summary>
    /// Sizes use the largest unit that keeps the number at one or more, and ignore the language of the machine.
    /// </summary>
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(-5, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1023.7, "1.0 KB")]
    [InlineData(1_048_575, "1.0 MB")]
    [InlineData(3_670_016, "3.5 MB")]
    [InlineData(5_368_709_120, "5.0 GB")]
    [InlineData(5_497_558_138_880, "5120.0 GB")]
    public void FormatSize_Bytes_UsesTheLargestFittingUnit(double bytes, string expected)
    {
        // Arrange
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            var text = UpdateDialogModel.FormatSize(bytes);

            // Assert
            text.Should().Be(expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>
    /// When the total is known the caption tells how much of it has arrived.
    /// </summary>
    [Fact]
    public void FormatProgress_KnownTotal_ShowsReceivedTotalAndSpeed()
    {
        // Arrange
        var progress = new DownloadProgress(1_048_576, 4_194_304, TimeSpan.FromSeconds(1));

        // Act
        var text = UpdateDialogModel.FormatProgress(progress);

        // Assert
        text.Should().Be("1.0 MB of 4.0 MB (1.0 MB/s)");
    }

    /// <summary>
    /// When the server does not report a length the caption still tells how much has arrived.
    /// </summary>
    [Fact]
    public void FormatProgress_UnknownTotal_ShowsReceivedAndSpeedOnly()
    {
        // Arrange
        var progress = new DownloadProgress(2_097_152, null, TimeSpan.FromSeconds(2));

        // Act
        var text = UpdateDialogModel.FormatProgress(progress);

        // Assert
        text.Should().Be("2.0 MB downloaded (1.0 MB/s)");
    }

    /// <summary>
    /// A failure of the installation is reported as one, whatever state the controller rests in.
    /// </summary>
    [Theory]
    [InlineData(UpdateFailureStage.Download)]
    [InlineData(UpdateFailureStage.Install)]
    public void Classify_InstallationFailure_IsFailed(UpdateFailureStage stage)
    {
        // Arrange
        var failure = new UpdateFailure(stage, new IOException("disk"), UserInitiated: true);

        // Act
        var outcome = UpdateDialogModel.Classify(failure, UpdateState.Available);

        // Assert
        outcome.Should().Be(UpdateInstallOutcome.Failed);
    }

    /// <summary>
    /// A failure of an earlier check says nothing about the installation.
    /// </summary>
    [Fact]
    public void Classify_CheckFailureOnly_IsIgnored()
    {
        // Arrange
        var failure = new UpdateFailure(UpdateFailureStage.Check, new IOException("net"), UserInitiated: false);

        // Act
        var outcome = UpdateDialogModel.Classify(failure, UpdateState.Available);

        // Assert
        outcome.Should().Be(UpdateInstallOutcome.Started);
    }

    /// <summary>
    /// A downloaded installer that is still waiting means it was not started, as when the permission prompt is declined.
    /// </summary>
    [Fact]
    public void Classify_DownloadedAndNoFailure_IsNotStarted()
    {
        UpdateDialogModel.Classify(null, UpdateState.Downloaded).Should().Be(UpdateInstallOutcome.NotStarted);
    }

    /// <summary>
    /// With no failure and no file left over the installer is on its way.
    /// </summary>
    [Fact]
    public void Classify_AvailableAndNoFailure_IsStarted()
    {
        UpdateDialogModel.Classify(null, UpdateState.Available).Should().Be(UpdateInstallOutcome.Started);
    }

    /// <summary>
    /// An installer that did not start without any error means the permission prompt was declined.
    /// </summary>
    [Fact]
    public void FailureText_NoError_SaysTheInstallerDidNotStart()
    {
        UpdateDialogModel.FailureText(null).Should().Be(Strings.UpdateNotStarted);
    }

    /// <summary>
    /// A failure is shown with the localized reason, never with the English message of the library.
    /// </summary>
    [Fact]
    public void FailureText_Error_ShowsTheLocalizedReason()
    {
        // Arrange
        var error = new ChecksumMismatchException("secret internal detail", "a", "b");

        // Act
        var text = UpdateDialogModel.FailureText(error);

        // Assert
        text.Should().Contain(Strings.UpdateErrorChecksum);
        text.Should().NotContain("secret internal detail");
    }

    /// <summary>
    /// A secure web address of the release is used as it is.
    /// </summary>
    [Fact]
    public void ReleasePageAddress_HttpsUrl_IsUsed()
    {
        // Act
        var address = UpdateDialogModel.ReleasePageAddress("https://example.com/releases/v1", new Uri("https://example.com/project"));

        // Assert
        address.Should().Be("https://example.com/releases/v1");
    }

    /// <summary>
    /// A missing address, or one that is not a secure web address, is replaced by the latest release page of the project.
    /// </summary>
    /// <param name="releaseUrl">
    /// The address the release gave.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://example.com/releases/v1")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-settings:privacy")]
    public void ReleasePageAddress_NotHttps_FallsBackToProjectPage(string? releaseUrl)
    {
        // Act
        var address = UpdateDialogModel.ReleasePageAddress(releaseUrl, new Uri("https://example.com/project/"));

        // Assert
        address.Should().Be("https://example.com/project/releases/latest");
    }
}
