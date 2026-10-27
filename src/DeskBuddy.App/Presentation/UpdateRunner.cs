namespace DeskBuddy.App.Presentation;

/// <summary>
/// Runs an update action that was started from a menu, a notice or a button.
/// </summary>
internal static class UpdateRunner
{
    /// <summary>
    /// Runs an update action. It is the end of an event handler, so it does not let any exception go.
    /// </summary>
    /// <param name="action">
    /// The action to run.
    /// </param>
    /// <param name="logger">
    /// Reports what went wrong.
    /// </param>
    /// <returns>
    /// A task that completes when the action is done.
    /// </returns>
    public static async Task RunAsync(Func<Task> action, ILogger logger)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The update could not be carried out.");
        }
    }
}
