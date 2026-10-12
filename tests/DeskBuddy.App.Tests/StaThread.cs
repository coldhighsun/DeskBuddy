using System.Runtime.ExceptionServices;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Runs code on a single-threaded-apartment thread, which WPF windows need.
/// </summary>
internal static class StaThread
{
    /// <summary>
    /// Runs an action on a new STA thread and waits for it; an exception it throws is thrown again here.
    /// </summary>
    /// <param name="action">
    /// The action to run.
    /// </param>
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
