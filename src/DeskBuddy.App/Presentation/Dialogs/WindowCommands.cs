using System.Windows;
using System.Windows.Input;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// The commands behind the buttons in the title bar of DeskBuddy's own borderless windows. They work on every
/// <see cref="Window"/>: the handlers are registered once, for the class.
/// </summary>
internal static class WindowCommands
{
    /// <summary>
    /// Closes the window the button is in.
    /// </summary>
    public static readonly RoutedUICommand Close = new("Close", nameof(Close), typeof(WindowCommands));

    /// <summary>
    /// Minimizes the window the button is in.
    /// </summary>
    public static readonly RoutedUICommand Minimize = new("Minimize", nameof(Minimize), typeof(WindowCommands));

    /// <summary>
    /// Registers the handlers of the commands for every window.
    /// </summary>
    static WindowCommands()
    {
        CommandManager.RegisterClassCommandBinding(
            typeof(Window),
            new CommandBinding(Close, (sender, _) => ((Window)sender).Close()));
        CommandManager.RegisterClassCommandBinding(
            typeof(Window),
            new CommandBinding(Minimize, (sender, _) => ((Window)sender).WindowState = WindowState.Minimized));
    }
}
