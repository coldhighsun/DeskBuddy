using GitHubReleaseUpdater.AutoUpdate;
using GitHubReleaseUpdater.Download;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// What the update dialog is showing.
/// </summary>
internal enum UpdateDialogState
{
    /// <summary>
    /// Asking whether to update.
    /// </summary>
    Prompt,

    /// <summary>
    /// Downloading the installer.
    /// </summary>
    Downloading,

    /// <summary>
    /// The last attempt failed or was declined; the user can try again.
    /// </summary>
    Failed,
}

/// <summary>
/// Which parts of the update dialog are visible in a state.
/// </summary>
/// <param name="ShowInfo">
/// Whether the explanation of what updating does is shown.
/// </param>
/// <param name="ShowProgress">
/// Whether the download progress is shown.
/// </param>
/// <param name="ShowError">
/// Whether the reason of the last failure is shown.
/// </param>
/// <param name="ShowUpdate">
/// Whether the button that starts (or, after a failure, repeats) the update is shown.
/// </param>
/// <param name="ShowSkip">
/// Whether the button that skips this version is shown.
/// </param>
/// <param name="ShowLater">
/// Whether the button that closes the dialog is shown.
/// </param>
/// <param name="ShowViewRelease">
/// Whether the button that opens the release page is shown.
/// </param>
/// <param name="ShowCancel">
/// Whether the button that stops the download is shown.
/// </param>
internal sealed record UpdateDialogLayout(
    bool ShowInfo,
    bool ShowProgress,
    bool ShowError,
    bool ShowUpdate,
    bool ShowSkip,
    bool ShowLater,
    bool ShowViewRelease,
    bool ShowCancel);

/// <summary>
/// How an installation that was asked for ended, as far as the dialog can tell.
/// </summary>
internal enum UpdateInstallOutcome
{
    /// <summary>
    /// Something went wrong; the controller holds the reason.
    /// </summary>
    Failed,

    /// <summary>
    /// The installer was downloaded but not started, which is what declining the permission prompt of Windows does.
    /// </summary>
    NotStarted,

    /// <summary>
    /// The installer was started and the application is about to end.
    /// </summary>
    Started,
}

/// <summary>
/// The parts of the update dialog that can be worked out without showing a window: which controls each state shows, the
/// texts of the download progress and of a failure, and the shortening of the release notes.
/// </summary>
internal static class UpdateDialogModel
{
    /// <summary>
    /// The longest run of release notes shown, so one oversized release body cannot make the dialog unwieldy.
    /// </summary>
    public const int MaxNotesLength = 4000;

    /// <summary>
    /// The units sizes are written in, from bytes upward.
    /// </summary>
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB"];

    /// <summary>
    /// Decides which controls a state shows.
    /// </summary>
    /// <param name="state">
    /// What the dialog is showing.
    /// </param>
    /// <param name="hasInstaller">
    /// Whether the release has an installer that can be downloaded; without one the user is sent to the release page.
    /// </param>
    /// <returns>
    /// The visibility of each part.
    /// </returns>
    public static UpdateDialogLayout LayoutFor(UpdateDialogState state, bool hasInstaller)
    {
        var prompt = state == UpdateDialogState.Prompt;
        var downloading = state == UpdateDialogState.Downloading;
        var failed = state == UpdateDialogState.Failed;

        return new UpdateDialogLayout(
            ShowInfo: prompt,
            ShowProgress: downloading,
            ShowError: failed,
            ShowUpdate: !downloading && hasInstaller,
            ShowSkip: prompt,
            ShowLater: !downloading,
            ShowViewRelease: !downloading && (failed || !hasInstaller),
            ShowCancel: downloading);
    }

    /// <summary>
    /// Shortens the release notes to what the dialog shows.
    /// </summary>
    /// <param name="notes">
    /// The body of the release, or <see langword="null"/> if it has none.
    /// </param>
    /// <returns>
    /// The trimmed notes, ended with an ellipsis if they were cut, or an empty string when there is nothing to show.
    /// </returns>
    public static string TrimNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return string.Empty;
        }

        var trimmed = notes.Trim();
        return trimmed.Length > MaxNotesLength ? trimmed[..MaxNotesLength] + "…" : trimmed;
    }

    /// <summary>
    /// Writes a number of bytes as a size a person can read, for example <c>3.5 MB</c>.
    /// </summary>
    /// <param name="bytes">
    /// The number of bytes; a negative number counts as zero.
    /// </param>
    /// <returns>
    /// The size with one decimal and the largest unit that keeps it at least one, always with a period as the decimal
    /// separator.
    /// </returns>
    public static string FormatSize(double bytes)
    {
        var value = Math.Max(0, bytes);
        var unit = 0;
        // Compared as it will be printed, so 1048575 bytes read "1.0 MB" and not "1024.0 KB".
        while (unit < SizeUnits.Length - 1 && Math.Round(value, unit == 0 ? 0 : 1) >= 1024)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{value:0} {SizeUnits[unit]}")
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.0} {SizeUnits[unit]}");
    }

    /// <summary>
    /// Writes the caption under the progress bar.
    /// </summary>
    /// <param name="progress">
    /// The state of the download.
    /// </param>
    /// <returns>
    /// The received size, the total size when it is known, and the speed, in the language of the interface.
    /// </returns>
    public static string FormatProgress(DownloadProgress progress)
    {
        var received = FormatSize(progress.BytesReceived);
        var speed = FormatSize(progress.BytesPerSecond);
        return progress.TotalBytes is { } total and > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.UpdateProgress, received, FormatSize(total), speed)
            : string.Format(CultureInfo.CurrentCulture, Strings.UpdateProgressUnknownTotal, received, speed);
    }

    /// <summary>
    /// Tells how an installation ended. Only valid for one that was started while <c>UpdateController.CanInstall</c> was true, since
    /// the controller returns without a trace from one that could not start.
    /// </summary>
    /// <param name="failure">
    /// The last failure of the controller, or <see langword="null"/> for none.
    /// </param>
    /// <param name="state">
    /// The state of the controller after the call.
    /// </param>
    /// <returns>
    /// The outcome; a failure of the check stage is not one of the installation.
    /// </returns>
    public static UpdateInstallOutcome Classify(UpdateFailure? failure, UpdateState state)
    {
        if (failure is { Stage: not UpdateFailureStage.Check })
        {
            return UpdateInstallOutcome.Failed;
        }

        return state == UpdateState.Downloaded ? UpdateInstallOutcome.NotStarted : UpdateInstallOutcome.Started;
    }

    /// <summary>
    /// Writes why an update that was started did not go through.
    /// </summary>
    /// <param name="error">
    /// What went wrong, or <see langword="null"/> when nothing did but the installer was not started, which is what
    /// happens when the user declines the permission prompt of Windows.
    /// </param>
    /// <returns>
    /// The sentence to show, in the language of the interface.
    /// </returns>
    public static string FailureText(Exception? error)
    {
        return error is null
            ? Strings.UpdateNotStarted
            : string.Format(CultureInfo.CurrentCulture, Strings.UpdateInstallFailed, UpdateErrorText.For(error));
    }

    /// <summary>
    /// Chooses the address the "release page" button opens. The address comes from the release the server described, so it is
    /// only used if it is a secure web address; anything else (a file, another program's scheme) is not handed to the shell.
    /// </summary>
    /// <param name="releaseUrl">
    /// The address of the release page, or <see langword="null"/> or empty if the release has none.
    /// </param>
    /// <param name="projectUri">
    /// The address of the project, whose latest release page is used instead.
    /// </param>
    /// <returns>
    /// The address to open.
    /// </returns>
    public static string ReleasePageAddress(string? releaseUrl, Uri projectUri)
    {
        ArgumentNullException.ThrowIfNull(projectUri);

        return Uri.TryCreate(releaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.AbsoluteUri
            : projectUri.AbsoluteUri.TrimEnd('/') + "/releases/latest";
    }
}
