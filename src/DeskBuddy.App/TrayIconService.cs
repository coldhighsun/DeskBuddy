using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Resources;
using DeskBuddy.App.Windows;
using DeskBuddy.Application;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace DeskBuddy.App;

/// <summary>
/// Owns the notification-area icon: its panel, show/hide and exit menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    /// <summary>
    /// The underlying notification-area icon.
    /// </summary>
    private readonly TaskbarIcon _icon;

    /// <summary>
    /// The icon the notification area shows; owned here and released on dispose.
    /// </summary>
    private readonly Icon _appIcon;

    /// <summary>
    /// The brain whose visibility the menu controls.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Opens the panel window.
    /// </summary>
    private readonly Action<PanelTab?> _openPanel;

    /// <summary>
    /// Callback invoked when the user chooses Exit.
    /// </summary>
    private readonly Action _onExit;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService"/> class and shows the icon.
    /// </summary>
    /// <param name="brain">
    /// The brain whose visibility the menu controls.
    /// </param>
    /// <param name="openPanel">
    /// Opens the panel window; called from the menu and on double-click of the icon.
    /// </param>
    /// <param name="onExit">
    /// Callback invoked when the user chooses Exit.
    /// </param>
    public TrayIconService(PetBrain brain, Action<PanelTab?> openPanel, Action onExit)
    {
        _brain = brain;
        _openPanel = openPanel;
        _onExit = onExit;

        _appIcon = AppIcon.LoadSmall();
        _icon = new TaskbarIcon
        {
            Icon = _appIcon,
            ToolTipText = Strings.AppName,
            ContextMenu = new ContextMenu(),
        };

        Refresh();
        _icon.ForceCreate();

        // The menu depends on pet visibility; rebuild it when that changes.
        _brain.VisibilityChanged += OnStatusChanged;
        _icon.TrayMouseDoubleClick += OnTrayDoubleClick;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _brain.VisibilityChanged -= OnStatusChanged;
        _icon.Dispose();
        _appIcon.Dispose();
    }

    /// <summary>
    /// Shows a balloon notice from the tray icon, flagged as an error.
    /// </summary>
    /// <param name="title">
    /// The notice title.
    /// </param>
    /// <param name="message">
    /// The notice text.
    /// </param>
    public void ShowErrorNotice(string title, string message)
    {
        _icon.ShowNotification(title, message, NotificationIcon.Error);
    }

    /// <summary>
    /// Opens the panel when the tray icon is double-clicked.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
    {
        _openPanel(null);
    }

    /// <summary>
    /// Rebuilds the menu when something it shows has changed.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnStatusChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    /// <summary>
    /// Rebuilds the context menu from whether the user hid the pet.
    /// </summary>
    private void Refresh()
    {
        var items = _icon.ContextMenu!.Items;
        items.Clear();

        var openPanel = new MenuItem { Header = Strings.OpenPanel };
        openPanel.Click += (_, _) => _openPanel(null);
        items.Add(openPanel);

        items.Add(new Separator());

        var toggle = new MenuItem { Header = _brain.Visibility.IsManuallyHidden ? Strings.TrayShowPet : Strings.TrayHidePet };
        toggle.Click += (_, _) => ToggleVisibility();
        items.Add(toggle);

        var exit = new MenuItem { Header = Strings.TrayExit };
        exit.Click += (_, _) => _onExit();
        items.Add(exit);
    }

    /// <summary>
    /// Hides the pet if the user has not hidden it, and shows it again otherwise.
    /// </summary>
    private void ToggleVisibility()
    {
        _brain.ToggleManuallyHidden();
    }
}
