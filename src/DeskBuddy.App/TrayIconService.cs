using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Resources;
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
    /// The pet window whose visibility is controlled from the tray.
    /// </summary>
    private readonly Window _petWindow;

    /// <summary>
    /// Called after any pomodoro command so the pet reacts immediately.
    /// </summary>
    private readonly Action _afterCommand;

    /// <summary>
    /// Opens the panel window.
    /// </summary>
    private readonly Action _openPanel;

    /// <summary>
    /// Callback invoked when the user chooses Exit.
    /// </summary>
    private readonly Action _onExit;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService"/> class and shows the icon.
    /// </summary>
    /// <param name="petWindow">
    /// The pet window whose visibility is controlled from the tray.
    /// </param>
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
    public TrayIconService(Window petWindow, PetBrain brain, Action afterCommand, Action openPanel, Action onExit)
    {
        _petWindow = petWindow;
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
        _petWindow.IsVisibleChanged += OnVisibleChanged;
        _icon.TrayMouseDoubleClick += OnTrayDoubleClick;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _brain.PomodoroStatusChanged -= OnStatusChanged;
        _petWindow.IsVisibleChanged -= OnVisibleChanged;
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
        _openPanel();
    }

    /// <summary>
    /// Rebuilds the menu when the pet is shown or hidden, so the toggle item reads correctly.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Refresh();
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
    /// Rebuilds the tooltip and the context menu from the current pomodoro status and pet visibility.
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
        openPanel.Click += (_, _) => _openPanel();
        items.Insert(0, openPanel);
        items.Insert(1, new Separator());

        items.Add(new Separator());

        var toggle = new MenuItem { Header = _petWindow.IsVisible ? Strings.TrayHidePet : Strings.TrayShowPet };
        toggle.Click += (_, _) => ToggleVisibility();
        items.Add(toggle);

        var exit = new MenuItem { Header = Strings.TrayExit };
        exit.Click += (_, _) => _onExit();
        items.Add(exit);
    }

    /// <summary>
    /// Shows the pet if hidden and hides it if shown.
    /// </summary>
    private void ToggleVisibility()
    {
        if (_petWindow.IsVisible)
        {
            _petWindow.Hide();
        }
        else
        {
            _petWindow.Show();
        }
    }
}
