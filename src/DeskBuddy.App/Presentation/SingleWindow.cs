using System.Windows;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Keeps at most one window of a kind open: it creates the window the first time it is needed, brings the same window
/// forward while it is open, and creates a new one after the user has closed it.
/// </summary>
/// <typeparam name="TWindow">
/// The kind of window.
/// </typeparam>
/// <param name="create">
/// Creates the window.
/// </param>
/// <param name="show">
/// Shows the window and brings it to the front.
/// </param>
public sealed class SingleWindow<TWindow>(Func<TWindow> create, Action<TWindow> show)
    where TWindow : Window
{
    /// <summary>
    /// The window that is open; <see langword="null"/> while there is none.
    /// </summary>
    private TWindow? _window;

    /// <summary>
    /// Shows the window, creating it if none is open.
    /// </summary>
    public void Open()
    {
        if (_window is null)
        {
            var window = create();
            window.Closed += OnWindowClosed;
            _window = window;
        }

        show(_window);
    }

    /// <summary>
    /// Forgets the window once it is closed, so that the next <see cref="Open"/> creates a new one.
    /// </summary>
    /// <param name="sender">
    /// The window that was closed.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _window = null;
    }
}
