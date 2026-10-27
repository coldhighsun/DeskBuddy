using GitHubReleaseUpdater.AutoUpdate;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Decides what the status line of the update controller says in the about window.
/// </summary>
internal static class UpdateStatusModel
{
    /// <summary>
    /// Gets the sentence that tells what the update controller is doing or found last.
    /// </summary>
    /// <param name="updates">
    /// The update controller.
    /// </param>
    /// <returns>
    /// The localized status, or an empty string if there is nothing to say.
    /// </returns>
    public static string For(UpdateController updates)
    {
        return updates.State switch
        {
            UpdateState.Checking => Strings.UpdateChecking,
            UpdateState.UpToDate => Strings.UpdateUpToDate,
            UpdateState.Failed => Strings.UpdateCheckFailed,
            UpdateState.Available when updates is { HasInstaller: false, Available: { } found } =>
                string.Format(CultureInfo.CurrentCulture, Strings.UpdateNoInstaller, found.Version),
            UpdateState.Available or UpdateState.Downloading or UpdateState.Downloaded or UpdateState.Installing
                when updates.Available is { } update =>
                string.Format(CultureInfo.CurrentCulture, Strings.UpdateAvailable, update.Version),
            _ => string.Empty,
        };
    }
}
