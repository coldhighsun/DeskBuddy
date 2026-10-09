using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Resources;
using DeskBuddy.App.Windows;
using DeskBuddy.Application;
using DeskBuddy.Domain.Pomodoro;
using H.NotifyIcon;

namespace DeskBuddy.App;

/// <summary>
/// Owns the notification-area icon: its pomodoro and show/hide/exit menu, and a tooltip with the pomodoro status.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    /// <summary>
    /// The underlying notification-area icon.
    /// </summary>
    private readonly TaskbarIcon _icon;

    /// <summary>
    /// The brain whose pomodoro the menu controls.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Called after any pomodoro command so the pet reacts immediately.
    /// </summary>
    private readonly Action _afterCommand;

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
    /// The brain whose pomodoro the menu controls.
    /// </param>
    /// <param name="afterCommand">
    /// Called after any pomodoro command so the pet reacts immediately.
    /// </param>
    /// <param name="openPanel">
    /// Opens the panel window; called from the menu and on double-click of the icon.
    /// </param>
    /// <param name="onExit">
    /// Callback invoked when the user chooses Exit.
    /// </param>
    public TrayIconService(PetBrain brain, Action afterCommand, Action<PanelTab?> openPanel, Action onExit)
    {
        _brain = brain;
        _afterCommand = afterCommand;
        _openPanel = openPanel;
        _onExit = onExit;

        _icon = new TaskbarIcon
        {
            IconSource = new GeneratedIconSource
            {
                Text = "D",
                Foreground = Brushes.White,
                Background = Brushes.DarkOrange,
            },
            ContextMenu = new ContextMenu(),
        };

        Refresh();
        _icon.ForceCreate();

        // The menu and tooltip depend on pet visibility and on the pomodoro; rebuild them when either changes.
        _brain.PomodoroStatusChanged += OnStatusChanged;
        _brain.VisibilityChanged += OnStatusChanged;
        _icon.TrayMouseDoubleClick += OnTrayDoubleClick;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _brain.PomodoroStatusChanged -= OnStatusChanged;
        _brain.VisibilityChanged -= OnStatusChanged;
        _icon.Dispose();
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
    /// Rebuilds the menu and tooltip when something they show has changed.
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
    /// Rebuilds the tooltip and the context menu from the current pomodoro status and whether the user hid the pet.
    /// </summary>
    private void Refresh()
    {
        var session = _brain.Pomodoro;
        _icon.ToolTipText = session.Phase == PomodoroPhase.Stopped
            ? Strings.AppName
            : $"{Strings.AppName} - {PomodoroMenu.FormatStatus(session)}";

        var items = _icon.ContextMenu!.Items;
        PomodoroMenu.Populate(items, _brain, _afterCommand);

        var openPanel = new MenuItem { Header = Strings.OpenPanel };
        openPanel.Click += (_, _) => _openPanel(null);
        items.Insert(0, openPanel);

        var settings = new MenuItem { Header = Strings.MenuSettings };
        settings.Click += (_, _) => _openPanel(PanelTab.Settings);
        items.Insert(1, settings);
        items.Insert(2, new Separator());

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
