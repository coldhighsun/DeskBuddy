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
public sealed partial class TrayIconService : IDisposable
{
    /// <summary>
    /// The edge of the square the picture of a pet is shown in, in the list of pets.
    /// </summary>
    private const double PetPictureEdge = 32;

    /// <summary>
    /// The room around the picture of a pet in the grid of pets, on each side.
    /// </summary>
    private const double PetGridCellPadding = 5;

    /// <summary>
    /// The width of one pet in the grid of pets: the picture, its frame (two units each side) and the room around it.
    /// </summary>
    private const double PetGridCellWidth = PetPictureEdge + 4 + (2 * PetGridCellPadding);

    /// <summary>
    /// The share of the height of the work area the grid of pets may take before the submenu scrolls.
    /// </summary>
    private const double PetGridMaxHeightShare = 0.6;

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
    /// What shows and hides the pet, and says whether it is hidden.
    /// </summary>
    private readonly IPetVisibilityControl _brain;

    /// <summary>
    /// Opens the panel window.
    /// </summary>
    private readonly Action<PanelTab?> _openPanel;

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
    /// <param name="brain">What shows and hides the pet, and says whether it is hidden.</param>
    /// <param name="openPanel">Opens the panel window; called from the menu and on double-click of the icon.</param>
    /// <param name="onExit">Callback invoked when the user chooses Exit.</param>
    /// <param name="settings">The settings, which store the pet and its size.</param>
    /// <param name="pets">The pets the menu offers.</param>
    /// <param name="updates">
    /// Looks for updates and installs them; the menu offers both. <see langword="null"/> leaves the entries out.
    /// </param>
    /// <param name="logger">
    /// Reports what went wrong when the pets could not be listed or a choice could not be saved.
    /// </param>
    internal TrayIconService(
        IPetVisibilityControl brain,
        Action<PanelTab?> openPanel,
        Action onExit,
        SettingsService settings,
        InstalledPets pets,
        UpdateController? updates,
        ILogger<TrayIconService> logger)
    {
        _brain = brain;
        _openPanel = openPanel;
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
    /// <param name="title">The notice title.</param>
    /// <param name="message">The notice text.</param>
    public void ShowErrorNotice(string title, string message)
    {
        _updateNoticeShown = false;
        _icon.ShowNotification(title, message, NotificationIcon.Error);
    }

    /// <summary>
    /// Opens the panel when the tray icon is double-clicked.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
    {
        _openPanel(null);
    }

    /// <summary>
    /// Builds the menu again when pet visibility changed.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private void OnStatusChanged(object? sender, EventArgs e)
    {
        RebuildIfNeeded();
    }

    /// <summary>
    /// Builds the menu again when the pet or its size changed, which may be reported on another thread.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="settings">The new settings.</param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        _icon.Dispatcher.BeginInvoke(() =>
        {
            UpdateIcon(_pets.Entries);
            RebuildIfNeeded();
        });
    }

    /// <summary>
    /// Writes the tooltip and the menu in the interface language now in use. Call on the UI thread after the language
    /// has been switched.
    /// </summary>
    public void RefreshLanguage()
    {
        _icon.ToolTipText = Strings.AppName;
        RebuildIfNeeded();
    }

    /// <summary>
    /// Hides the pet if the user has not hidden it, and shows it again otherwise.
    /// </summary>
    private void ToggleVisibility()
    {
        _brain.ToggleManuallyHidden();
    }
}
