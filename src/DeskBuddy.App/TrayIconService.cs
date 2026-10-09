using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;

namespace DeskBuddy.App;

/// <summary>
/// Owns the notification-area icon and its context menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    /// <summary>
    /// The underlying notification-area icon.
    /// </summary>
    private readonly TaskbarIcon _icon;

    /// <summary>
    /// Menu item that toggles pet visibility; its header changes with the current state.
    /// </summary>
    private readonly MenuItem _toggleItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService"/> class and shows the icon.
    /// </summary>
    /// <param name="petWindow">
    /// The pet window whose visibility is controlled from the tray.
    /// </param>
    /// <param name="onExit">
    /// Callback invoked when the user chooses Exit.
    /// </param>
    public TrayIconService(Window petWindow, Action onExit)
    {
        _toggleItem = new MenuItem { Header = "Hide pet" };
        _toggleItem.Click += (_, _) => ToggleVisibility(petWindow);

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => onExit();

        _icon = new TaskbarIcon
        {
            ToolTipText = "DeskBuddy",
            IconSource = new GeneratedIconSource
            {
                Text = "D",
                Foreground = Brushes.White,
                Background = Brushes.DarkOrange,
            },
            ContextMenu = new ContextMenu { Items = { _toggleItem, new Separator(), exitItem } },
        };
        _icon.ForceCreate();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _icon.Dispose();
    }

    /// <summary>
    /// Shows the pet if hidden and hides it if shown, updating the menu header accordingly.
    /// </summary>
    /// <param name="petWindow">
    /// The pet window to toggle.
    /// </param>
    private void ToggleVisibility(Window petWindow)
    {
        if (petWindow.IsVisible)
        {
            petWindow.Hide();
            _toggleItem.Header = "Show pet";
        }
        else
        {
            petWindow.Show();
            _toggleItem.Header = "Hide pet";
        }
    }
}
