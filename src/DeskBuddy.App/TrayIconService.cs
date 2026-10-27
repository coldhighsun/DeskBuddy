using Icon = System.Drawing.Icon;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using DeskBuddy.App.Windows;
using GitHubReleaseUpdater.AutoUpdate;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace DeskBuddy.App;

/// <summary>
/// Owns the notification-area icon: its panel, show/hide, pet and size choice, and exit menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    /// <summary>
    /// The edge of the square the picture of a pet is shown in, in the list of pets.
    /// </summary>
    private const double PetPictureEdge = 32;

    /// <summary>
    /// The underlying notification-area icon.
    /// </summary>
    private readonly TaskbarIcon _icon;

    /// <summary>
    /// The application's own icon, which the notification area shows when the pet in use has no picture; owned here and
    /// released on dispose.
    /// </summary>
    private readonly Icon _appIcon;

    /// <summary>
    /// The icon made of the pet in use, shown in the notification area while there is one; owned here and released when
    /// it is replaced and on dispose.
    /// </summary>
    private Icon? _petIcon;

    /// <summary>
    /// The picture the notification-area icon was made from; <see langword="null"/> while it shows the application icon.
    /// </summary>
    private ImageSource? _iconPicture;

    /// <summary>
    /// Whether the icon has been put in the notification area. That waits until the pets are known, so the icon appears
    /// as the pet in use from the start, instead of as the application icon that changes a moment later.
    /// </summary>
    private bool _iconShown;

    /// <summary>
    /// Whether the service has been disposed, after which the icon is not shown any more.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// The brain whose visibility the menu controls.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Opens the panel window.
    /// </summary>
    private readonly Action<PanelTab?> _openPanel;

    /// <summary>
    /// Opens the about window.
    /// </summary>
    private readonly Action _openAbout;

    /// <summary>
    /// Callback invoked when the user chooses Exit.
    /// </summary>
    private readonly Action _onExit;

    /// <summary>
    /// Looks for updates and installs them; the menu offers both. <see langword="null"/> if updates are not available in this build.
    /// </summary>
    private readonly UpdateController? _updates;

    /// <summary>
    /// Whether the notice last shown announces a new version, which installs it when clicked.
    /// </summary>
    private bool _updateNoticeShown;

    /// <summary>
    /// The settings, which store the pet and its size.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// The pets the menu offers.
    /// </summary>
    private readonly InstalledPets _pets;

    /// <summary>
    /// Reports what went wrong when the pets could not be listed or a choice could not be saved.
    /// </summary>
    private readonly ILogger<TrayIconService> _logger;

    /// <summary>
    /// Decides when the menu is built again.
    /// </summary>
    private readonly TrayMenuRebuildPolicy _rebuildPolicy = new();

    /// <summary>
    /// An invisible window shown while the menu is open so that the application is the active one; created on first use.
    /// </summary>
    private Window? _anchor;

    /// <summary>
    /// The window of another application that was in front when the menu opened, to put back in front when the menu is
    /// closed without anything else taking the focus; 0 if there is none.
    /// </summary>
    private nint _previousForeground;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService"/> class and shows the icon.
    /// </summary>
    /// <param name="brain">
    /// The brain whose visibility the menu controls.
    /// </param>
    /// <param name="openPanel">
    /// Opens the panel window; called from the menu and on double-click of the icon.
    /// </param>
    /// <param name="openAbout">
    /// Opens the about window; called from the menu.
    /// </param>
    /// <param name="onExit">
    /// Callback invoked when the user chooses Exit.
    /// </param>
    /// <param name="theme">
    /// Gives the context menu the colors of the current theme.
    /// </param>
    /// <param name="settings">
    /// The settings, which store the pet and its size.
    /// </param>
    /// <param name="pets">
    /// The pets the menu offers.
    /// </param>
    /// <param name="updates">
    /// Looks for updates and installs them; the menu offers both. <see langword="null"/> leaves the entries out.
    /// </param>
    /// <param name="logger">
    /// Reports what went wrong when the pets could not be listed or a choice could not be saved.
    /// </param>
    internal TrayIconService(
        PetBrain brain,
        Action<PanelTab?> openPanel,
        Action openAbout,
        Action onExit,
        ThemeService theme,
        SettingsService settings,
        InstalledPets pets,
        UpdateController? updates,
        ILogger<TrayIconService> logger)
    {
        _brain = brain;
        _openPanel = openPanel;
        _openAbout = openAbout;
        _onExit = onExit;
        _settings = settings;
        _pets = pets;
        _updates = updates;
        _logger = logger;

        _appIcon = AppIcon.LoadSmall();
        _icon = new TaskbarIcon
        {
            Icon = _appIcon,
            ToolTipText = Strings.AppName,
            ContextMenu = new ContextMenu(),
        };

        theme.Register(_icon.ContextMenu);
        Refresh();

        // The menu depends on pet visibility, the pet and its size; build it again when one of them changes.
        _brain.VisibilityChanged += OnStatusChanged;
        _settings.Changed += OnSettingsChanged;
        if (_updates is not null)
        {
            _updates.Changed += OnStatusChanged;
        }

        _icon.TrayBalloonTipClicked += OnBalloonTipClicked;
        _icon.TrayMouseDoubleClick += OnTrayDoubleClick;
        _icon.ContextMenu.Opened += OnMenuOpened;
        _icon.PreviewTrayContextMenuOpen += OnPreviewMenuOpen;
        _icon.ContextMenu.Closed += OnMenuClosed;

        _ = ShowIconForPetInUseAsync();
        _ = RefreshPetsAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _brain.VisibilityChanged -= OnStatusChanged;
        _settings.Changed -= OnSettingsChanged;
        if (_updates is not null)
        {
            _updates.Changed -= OnStatusChanged;
        }

        _icon.TrayBalloonTipClicked -= OnBalloonTipClicked;
        _anchor?.Close();
        _icon.Dispose();
        _petIcon?.Dispose();
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
        _updateNoticeShown = false;
        _icon.ShowNotification(title, message, NotificationIcon.Error);
    }

    /// <summary>
    /// Wraps a picture for a menu entry in a frame that is drawn in the accent color for the entry in use and invisible
    /// for the others, so every picture takes the same room.
    /// </summary>
    /// <param name="picture">
    /// The picture.
    /// </param>
    /// <param name="width">
    /// The width of the picture.
    /// </param>
    /// <param name="height">
    /// The height of the picture.
    /// </param>
    /// <param name="isCurrent">
    /// Whether the entry is the one in use.
    /// </param>
    /// <returns>
    /// The framed picture.
    /// </returns>
    private static Border Framed(ImageSource? picture, double width, double height, bool isCurrent)
    {
        return new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = isCurrent ? SystemColors.HighlightBrush : Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Child = new System.Windows.Controls.Image { Source = picture, Width = width, Height = height },
        };
    }

    /// <summary>
    /// Moves the submenu of an item so that its first entry is level with the item. The theme opens it higher than that,
    /// because the popup has a margin, a border and padding above its entries; the offset is worked out from the
    /// submenu's own template, so nothing depends on how big the theme makes them. A template without the parts the
    /// offset is worked out from is left as it is.
    /// </summary>
    /// <param name="item">
    /// The item that has the submenu.
    /// </param>
    private static void AlignSubmenu(MenuItem item)
    {
        item.Loaded += (_, _) =>
        {
            if (item.Template.FindName("PART_Popup", item) is Popup popup && item.Template.FindName("SubmenuBorder", item) is Border border)
            {
                popup.VerticalOffset = -(border.Margin.Top + border.BorderThickness.Top + border.Padding.Top);
            }
        };
    }

    /// <summary>
    /// Opens the menu at the mouse, instead of the notification-area icon. The menu needs the application to be the active
    /// one with an active window, as when it opens inside any window: the icon brought the menu's own window to the front
    /// after opening it, and that switch between two windows of the application released the mouse capture the menu
    /// depends on, so the menu closed at once. Here the application is made active first, with an invisible window that
    /// is put under the mouse, and the menu is opened relative to that window, which also leaves the conversion between
    /// device pixels and the scaling of the monitor under the mouse to WPF.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data; marked as handled so the icon does not open the menu as well.
    /// </param>
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
    /// <param name="x">
    /// The horizontal position in device pixels of the screen.
    /// </param>
    /// <param name="y">
    /// The vertical position in device pixels of the screen.
    /// </param>
    /// <returns>
    /// The window.
    /// </returns>
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
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
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
    /// Shows a balloon notice from the tray icon.
    /// </summary>
    /// <param name="title">
    /// The notice title.
    /// </param>
    /// <param name="message">
    /// The notice text.
    /// </param>
    /// <param name="installsUpdate">
    /// <see langword="true"/> if clicking the notice should offer to install the version that was found.
    /// </param>
    public void ShowInfoNotice(string title, string message, bool installsUpdate = false)
    {
        _updateNoticeShown = installsUpdate;
        _icon.ShowNotification(title, message, NotificationIcon.Info);
    }

    /// <summary>
    /// Offers to install the new version when the notice announcing it is clicked.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        if (_updateNoticeShown)
        {
            _updateNoticeShown = false;
            _ = RunUpdateAsync(InstallUpdateAsync);
        }
    }

    /// <summary>
    /// Asks the user whether to install the version that was found, and installs it. Does nothing without an update
    /// controller.
    /// </summary>
    /// <returns>
    /// A task that completes when the installation was started, declined or failed.
    /// </returns>
    private Task InstallUpdateAsync()
    {
        return _updates is { } updates ? updates.InstallAsync() : Task.CompletedTask;
    }

    /// <summary>
    /// Runs an update action started from the menu or a notice.
    /// </summary>
    /// <param name="action">
    /// The action to run.
    /// </param>
    /// <returns>
    /// A task that completes when the action is done.
    /// </returns>
    private Task RunUpdateAsync(Func<Task> action)
    {
        return UpdateRunner.RunAsync(action, _logger);
    }

    /// <summary>
    /// Builds the menu again when pet visibility changed.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnStatusChanged(object? sender, EventArgs e)
    {
        RebuildIfNeeded();
    }

    /// <summary>
    /// Builds the menu again when the pet or its size changed, which may be reported on another thread.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        _icon.Dispatcher.BeginInvoke(() =>
        {
            UpdateIcon(_pets.Entries);
            RebuildIfNeeded();
        });
    }

    /// <summary>
    /// Looks for pets installed or repaired since the menu was last opened. The menu shows them the next time it opens.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private async void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        await RefreshPetsAsync();
    }

    /// <summary>
    /// Looks for pets again and builds the menu again if that changed the list. Never throws, as it is started without
    /// anything waiting for it: a failure only leaves the list as it was.
    /// </summary>
    /// <returns>
    /// A task that completes when the pets have been looked at.
    /// </returns>
    private async Task RefreshPetsAsync()
    {
        try
        {
            if (await _pets.RefreshAsync())
            {
                RebuildIfNeeded();
            }

            UpdateIcon(_pets.Entries);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The pets of the tray menu could not be listed.");
        }
        finally
        {
            ShowIcon();
        }
    }

    /// <summary>
    /// Puts the icon in the notification area, the first time it is called, showing the pet in use if it is known by now
    /// (and the application icon if the pets could not be listed).
    /// </summary>
    private void ShowIcon()
    {
        if (_iconShown || _disposed)
        {
            return;
        }

        UpdateIcon(_pets.Entries);
        _icon.ForceCreate();
        _iconShown = true;
    }

    /// <summary>
    /// Puts the icon in the notification area as soon as the pet in use is loaded, without waiting for the other pets to be
    /// loaded: only that pet is needed for the icon. If it cannot be loaded here, the icon is put there when the whole list
    /// has been looked at. Never throws, as it is started without anything waiting for it.
    /// </summary>
    /// <returns>
    /// A task that completes when the icon has been put in the notification area or it was found that it cannot be yet.
    /// </returns>
    private async Task ShowIconForPetInUseAsync()
    {
        try
        {
            var entry = await _pets.LoadAsync(_settings.Current.PetName);
            if (entry is not null && !_iconShown && !_disposed)
            {
                UpdateIcon([entry]);
                _icon.ForceCreate();
                _iconShown = true;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The tray icon could not be made from the pet in use ahead of the other pets.");
        }
    }

    /// <summary>
    /// Makes the notification-area icon show the pet in use (the icon picture the pet brings, else its idle picture), or the
    /// application icon if that pet has no picture; does nothing if the icon already shows the picture of that pet. Never
    /// throws: if the picture cannot be made into an icon, the icon stays as it was.
    /// </summary>
    /// <param name="entries">
    /// The pets to take the pet in use from: all of them, or only the pet in use while the others are still being loaded.
    /// </param>
    private void UpdateIcon(IReadOnlyList<InstalledPet> entries)
    {
        var choice = TrayIconModel.Choose(entries, _settings.Current.PetName, PetSettings.DefaultPetName);
        var picture = choice.Picture;
        if (ReferenceEquals(picture, _iconPicture))
        {
            return;
        }

        try
        {
            var icon = picture is null ? null : choice.IsIconPicture ? AppIcon.FromIconPicture(picture) : AppIcon.FromPicture(picture);
            _icon.Icon = icon ?? _appIcon;
            _petIcon?.Dispose();
            _petIcon = icon;
            _iconPicture = picture;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The tray icon could not be made from the picture of the pet.");
        }
    }

    /// <summary>
    /// Builds the menu again if what it shows changed, and it is not open.
    /// </summary>
    private void RebuildIfNeeded()
    {
        if (_rebuildPolicy.ShouldRebuild(CurrentState(), _icon.ContextMenu!.IsOpen))
        {
            Refresh();
        }
    }

    /// <summary>
    /// Gets what the menu has to show now.
    /// </summary>
    /// <returns>
    /// The state of the menu.
    /// </returns>
    private TrayMenuState CurrentState()
    {
        var settings = _settings.Current;
        return new TrayMenuState(settings.PetName,
            settings.Size,
            _brain.Visibility.IsManuallyHidden,
            _pets.Version,
            _updates is { CanInstall: true, Available: { } found } ? found.Version.ToString() : null);
    }

    /// <summary>
    /// Builds the submenu that lists the pets with their pictures; the picture of the pet in use has a frame in the
    /// accent color. The entries have no text, only a tooltip, except for a pet that has no picture to show: it is listed
    /// by its name, checked if it is the pet in use.
    /// </summary>
    /// <param name="state">
    /// What the menu has to show.
    /// </param>
    /// <returns>
    /// The submenu item.
    /// </returns>
    private MenuItem BuildPetsMenu(TrayMenuState state)
    {
        var menu = new MenuItem { Header = Strings.TraySwitchPet };
        foreach (var choice in TrayMenuModel.PetChoices(_pets.Entries, state.PetName))
        {
            var item = new MenuItem { ToolTip = choice.DisplayName };
            AutomationProperties.SetName(item, choice.DisplayName);
            if (choice.Picture is null)
            {
                item.Header = choice.DisplayName;
                item.IsChecked = choice.IsCurrent;
            }
            else
            {
                item.Icon = Framed(choice.Picture, PetPictureEdge, PetPictureEdge, choice.IsCurrent);
            }

            item.Click += async (_, _) => await ChangeSettingsAsync(settings => settings with { PetName = choice.FolderName });
            menu.Items.Add(item);
        }

        menu.IsEnabled = menu.Items.Count > 0;
        AlignSubmenu(menu);
        return menu;
    }

    /// <summary>
    /// Builds the submenu that changes the pet size. Each entry shows the pet in use at the size the entry gives it on
    /// screen; the picture of the size in use has a frame in the accent color. The entries have no text, only a tooltip.
    /// </summary>
    /// <param name="state">
    /// What the menu has to show.
    /// </param>
    /// <returns>
    /// The submenu item.
    /// </returns>
    private MenuItem BuildSizeMenu(TrayMenuState state)
    {
        var menu = new MenuItem { Header = Strings.TrayPetSize };
        foreach (var choice in PetSizeChoices.For(_pets.Entries, state.PetName, state.Size))
        {
            var label = choice.Label;
            var item = new MenuItem { ToolTip = label, IsChecked = choice.Picture is null && choice.IsCurrent };
            AutomationProperties.SetName(item, label);
            if (choice.Picture is null)
            {
                item.Header = label;
            }
            else
            {
                item.Icon = Framed(choice.Picture, choice.Width, choice.Height, choice.IsCurrent);
            }

            item.Click += async (_, _) => await ChangeSettingsAsync(settings => settings with { Size = choice.Size });
            menu.Items.Add(item);
        }

        AlignSubmenu(menu);
        return menu;
    }

    /// <summary>
    /// Saves a settings change made from the menu; a failure to save is reported as a notice. It is the end of an
    /// <see langword="async"/> event handler, so it does not let any exception go.
    /// </summary>
    /// <param name="change">
    /// Produces the new settings from the current ones.
    /// </param>
    /// <returns>
    /// A task that completes when the change has been saved.
    /// </returns>
    private async Task ChangeSettingsAsync(Func<PetSettings, PetSettings> change)
    {
        try
        {
            await _settings.UpdateAsync(change);
        }
        catch (InvalidOperationException)
        {
            // Raised only after a failed pet load; the panel shows why, so there is nothing to add.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The choice made in the tray menu could not be saved.");
            ShowErrorNotice(Strings.AppName, string.Format(CultureInfo.CurrentCulture, Strings.SettingsSaveFailed, ex.Message));
        }
    }

    /// <summary>
    /// Builds the context menu from whether the user hid the pet, the pet in use and its size.
    /// </summary>
    private void Refresh()
    {
        var state = CurrentState();
        var items = _icon.ContextMenu!.Items;
        items.Clear();

        var openSettings = new MenuItem { Header = Strings.TraySettings };
        openSettings.Click += (_, _) => _openPanel(null);
        items.Add(openSettings);

        items.Add(BuildPetsMenu(state));
        items.Add(BuildSizeMenu(state));

        items.Add(new Separator());

        var toggle = new MenuItem { Header = state.PetHidden ? Strings.TrayShowPet : Strings.TrayHidePet };
        toggle.Click += (_, _) => ToggleVisibility();
        items.Add(toggle);

        if (state.UpdateVersion is { } version)
        {
            var install = new MenuItem { Header = string.Format(CultureInfo.CurrentCulture, Strings.TrayUpdateTo, version) };
            install.Click += (_, _) => _ = RunUpdateAsync(InstallUpdateAsync);
            items.Add(install);
        }

        if (_updates is not null)
        {
            var checkUpdates = new MenuItem { Header = Strings.TrayCheckUpdates };
            checkUpdates.Click += (_, _) => _ = RunUpdateAsync(() => _updates.CheckAsync(manual: true));
            items.Add(checkUpdates);
        }

        var about = new MenuItem { Header = Strings.TrayAbout };
        about.Click += (_, _) => _openAbout();
        items.Add(about);

        var exit = new MenuItem { Header = Strings.TrayExit };
        exit.Click += (_, _) => _onExit();
        items.Add(exit);

        _rebuildPolicy.MarkBuilt(state);
    }

    /// <summary>
    /// Hides the pet if the user has not hidden it, and shows it again otherwise.
    /// </summary>
    private void ToggleVisibility()
    {
        _brain.ToggleManuallyHidden();
    }
}
