namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Writes what happened to the settings at startup to the log.
/// </summary>
internal static class SettingsLog
{
    /// <summary>
    /// Logs each process name that was taken out of a list when the settings were loaded.
    /// </summary>
    /// <param name="logger">
    /// Where to write.
    /// </param>
    /// <param name="removed">
    /// The names that were taken out.
    /// </param>
    public static void LogRemovedProcessNames(ILogger logger, IReadOnlyList<RemovedProcessName> removed)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(removed);

        foreach (var item in removed)
        {
            if (item.Reason == ProcessListProblem.ContainsSpace)
            {
                logger.LogWarning(
                    "Process name \"{Name}\" was removed from the {List} list when the settings were loaded: it has a space.",
                    item.Name,
                    item.List);
            }
            else
            {
                logger.LogWarning(
                    "Process name \"{Name}\" was removed from the {List} list when the settings were loaded: it is already in the {KeptIn} list.",
                    item.Name,
                    item.List,
                    item.KeptIn);
            }
        }
    }
}
