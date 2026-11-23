using System.Windows;
using System.Windows.Threading;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Runs a window on a UI thread of its own, so that whatever blocks that thread (a call into Windows that waits for the
/// window manager, which can happen when the system is busy) stops that window only, and not the tray icon, the panel
/// or the rest of the application. Everything that touches the window goes through <see cref="Post(Action{T})"/> or
/// <see cref="InvokeAsync{TResult}"/>; the thread that asks never waits for the window's thread, except in
/// <see cref="Shutdown"/>, and then only for as long as it is told to.
/// </summary>
/// <typeparam name="T">The kind of window the thread runs.</typeparam>
public sealed class UiThreadHost<T> : IDisposable
    where T : Window
{
    /// <summary>
    /// The thread; it ends when its dispatcher is shut down.
    /// </summary>
    private readonly Thread _thread;

    /// <summary>
    /// Completes when the window has been made, or fails with what made it fail.
    /// </summary>
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// What is done with an error nobody caught on the thread; says whether to carry on.
    /// </summary>
    private readonly Func<Exception, bool> _onUnhandled;

    /// <summary>
    /// The dispatcher of the thread; <see langword="null"/> until the thread has started, and again if it ended because the
    /// window could not be made.
    /// </summary>
    private volatile Dispatcher? _dispatcher;

    /// <summary>
    /// The window; only to be touched on the thread. <see langword="null"/> until it has been made.
    /// </summary>
    private T? _window;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiThreadHost{T}"/> class and starts the thread.
    /// </summary>
    /// <param name="name">The name of the thread, for the debugger and for dumps.</param>
    /// <param name="create">
    /// Makes the window; runs on the new thread. It shows the window itself if the window is to be shown.
    /// </param>
    /// <param name="onUnhandled">
    /// Decides what happens to an error nobody caught on the thread: <see langword="true"/> to carry on.
    /// </param>
    /// <param name="prepare">Runs first on the new thread, before the window is made; <see langword="null"/> for nothing.</param>
    public UiThreadHost(string name, Func<T> create, Func<Exception, bool> onUnhandled, Action? prepare = null)
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(onUnhandled);

        _onUnhandled = onUnhandled;
        _thread = new Thread(() => Run(create, prepare)) { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>
    /// Gets a task that completes once the window has been made on its thread, or fails with the error that stopped it.
    /// </summary>
    public Task Started => _started.Task;

    /// <summary>
    /// Gets the dispatcher of the thread, for a watchdog that probes it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The thread has not started yet (wait for <see cref="Started"/>), or it has ended because the window could not be
    /// made.
    /// </exception>
    public Dispatcher Dispatcher =>
        _dispatcher ?? throw new InvalidOperationException("The thread is not running; see Started for why it may have failed.");

    /// <summary>
    /// Hands work on the window to its thread, without waiting for it.
    /// </summary>
    /// <param name="action">The work; it runs on the window's thread.</param>
    public void Post(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // A thread that has ended no longer takes work; there is nothing to do for it then.
        _dispatcher?.BeginInvoke(() => action(_window!));
    }

    /// <summary>
    /// Hands work that does not need the window to the thread, without waiting for it.
    /// </summary>
    /// <param name="action">The work; it runs on the window's thread.</param>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _dispatcher?.BeginInvoke(action);
    }

    /// <summary>
    /// Runs work on the window's thread and gives back what it returns, without holding the caller's thread.
    /// </summary>
    /// <typeparam name="TResult">What the work returns.</typeparam>
    /// <param name="function">The work; it runs on the window's thread.</param>
    /// <returns>A task that completes with the result.</returns>
    public async Task<TResult> InvokeAsync<TResult>(Func<T, TResult> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        var dispatcher = Dispatcher;
        return await dispatcher.InvokeAsync(() => function(_window!));
    }

    /// <summary>
    /// Runs asynchronous work on the window's thread and waits for it to finish, without holding the caller's thread.
    /// </summary>
    /// <param name="function">The work; it starts on the window's thread.</param>
    /// <returns>A task that completes when the work has.</returns>
    public async Task InvokeAsync(Func<T, Task> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        var dispatcher = Dispatcher;
        var work = await dispatcher.InvokeAsync(() => function(_window!));
        await work;
    }

    /// <summary>
    /// Closes the window and ends the thread.
    /// </summary>
    /// <param name="wait">How long to wait for the thread to end; it is a background thread, so it does not hold the process.</param>
    public void Shutdown(TimeSpan wait)
    {
        if (_dispatcher is not { } dispatcher || dispatcher.HasShutdownStarted)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            _window?.Close();
            dispatcher.InvokeShutdown();
        });
        _thread.Join(wait);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Shutdown(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The body of the thread: makes the window, then pumps messages until shut down.
    /// </summary>
    /// <param name="create">Makes the window.</param>
    /// <param name="prepare">Runs first, or <see langword="null"/>.</param>
    private void Run(Func<T> create, Action? prepare)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.UnhandledException += (_, args) => args.Handled = _onUnhandled(args.Exception);
        _dispatcher = dispatcher;

        try
        {
            prepare?.Invoke();
            _window = create();
        }
        catch (Exception exception)
        {
            // The thread ends here, so its dispatcher takes no more work: let Dispatcher refuse it and Post do nothing,
            // instead of queueing work that never runs and leaving a caller that awaits it waiting for good. This comes
            // before Started fails, so that whoever wakes up on it already finds the host closed.
            _dispatcher = null;

            // Not handled here: the error is handed on to whoever awaits Started, which decides what it means.
            _started.SetException(exception);
            return;
        }

        _started.SetResult();
        Dispatcher.Run();
    }
}
