using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace DeskBuddy.App;

/// <summary>
/// The part of the tray icon service that opens the menu at the mouse and gives the focus back when it closes.
/// </summary>
public sealed partial class TrayIconService
{
    /// <summary>
    /// Opens the menu at the mouse, instead of the notification-area icon. The menu needs the application to be the active
    /// one with an active window, as when it opens inside any window: the icon brought the menu's own window to the front
    /// after opening it, and that switch between two windows of the application released the mouse capture the menu
    /// depends on, so the menu closed at once. Here the application is made active first, with an invisible window that
    /// is put under the mouse, and the menu is opened relative to that window, which also leaves the conversion between
    /// device pixels and the scaling of the monitor under the mouse to WPF.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data; marked as handled so the icon does not open the menu as well.</param>
    private void OnPreviewMenuOpen(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var (x, y) = MonitorHelper.GetCursorPositionPixels();
        var anchor = ActivateAnchor(x, y);
        var menu = _icon.ContextMenu!;
        menu.PlacementTarget = anchor;
        menu.Placement = PlacementMode.Relative;
        menu.HorizontalOffset = 0;
        menu.VerticalOffset = 0;
        menu.IsOpen = true;
    }

    /// <summary>
    /// Makes the application the active one by showing an invisible window at a place on the screen and activating it.
    /// </summary>
    /// <param name="x">The horizontal position in device pixels of the screen.</param>
    /// <param name="y">The vertical position in device pixels of the screen.</param>
    /// <returns>The window.</returns>
    private Window ActivateAnchor(int x, int y)
    {
        _anchor ??= new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Width = 1,
            Height = 1,
        };

        var front = ForegroundWindowHelper.Get();
        if (front != 0 && !ForegroundWindowHelper.BelongsToThisProcess(front))
        {
            _previousForeground = front;
        }

        WindowMover.MoveToPixelsSettled(_anchor, x, y);
        _anchor.Show();
        _anchor.Activate();
        return _anchor;
    }

    /// <summary>
    /// Hides the window that kept the application active while the menu was open, gives the focus back to the application
    /// that had it if nothing else took it (a click on another window, or a window opened by a menu entry, has already
    /// moved it), and builds the menu again if something it shows changed in the meantime.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private void OnMenuClosed(object sender, RoutedEventArgs e)
    {
        if (_anchor is not null)
        {
            var anchorInFront = ForegroundWindowHelper.Get() == new WindowInteropHelper(_anchor).Handle;
            _anchor.Hide();
            if (anchorInFront && _previousForeground != 0)
            {
                ForegroundWindowHelper.TryActivate(_previousForeground);
            }
        }

        _previousForeground = 0;
        RebuildIfNeeded();
    }

    /// <summary>
    /// Looks for pets installed or repaired since the menu was last opened. The menu shows them the next time it opens.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private async void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        await RefreshPetsAsync();
    }
}
