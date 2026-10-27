using GitHubReleaseUpdater.Exceptions;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Turns what went wrong while updating into a sentence in the language of the interface. The messages of the update
/// library are in English and meant for logs, so they are never shown to the user.
/// </summary>
internal static class UpdateErrorText
{
    /// <summary>
    /// Gets the sentence that tells the user why an update could not be installed.
    /// </summary>
    /// <param name="error">
    /// What went wrong.
    /// </param>
    /// <returns>
    /// The localized reason.
    /// </returns>
    public static string For(Exception error)
    {
        return error switch
        {
            ChecksumMismatchException => Strings.UpdateErrorChecksum,
            AssetNotFoundException => Strings.UpdateNoInstallerNotice,
            InstallerLaunchException => Strings.UpdateErrorLaunch,

            // The library throws the plain base type for a download it cannot vouch for: no checksum to compare with, a size
            // that differs from the release's or a body that was cut short. Its subclasses are matched above.
            UpdaterException when error.GetType() == typeof(UpdaterException) => Strings.UpdateErrorUnverified,
            _ => Strings.UpdateErrorGeneric,
        };
    }
}
