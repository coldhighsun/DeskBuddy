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
        // WPF registers the pack:// scheme, which the resource dictionaries are addressed with, when its Application type
        // is first used. Touching a static member does that without creating an Application, of which a process may only
        // have one.
        _ = System.Windows.Application.ResourceAssembly;

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
