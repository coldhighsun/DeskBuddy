using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.App.ViewModels;

/// <content>
/// Start at sign-in, the boss key and the log directory.
/// </content>
public sealed partial class SettingsTabViewModel
{
    /// <summary>
    /// The boss key registration, to find out whether a new shortcut could be registered.
    /// </summary>
    private readonly BossKeyController _bossKey;

    /// <summary>
    /// Starts the application at sign-in.
    /// </summary>
    private readonly IAutoStartService _autoStartService;

    /// <summary>
    /// Opens the directory the log files are written to.
    /// </summary>
    private readonly Action _openLogDirectory;

    /// <summary>
    /// Whether a new boss key is being tried, during which the controller's status changes are handled by that attempt.
    /// </summary>
    private bool _applyingBossKey;

    /// <summary>
    /// Whether the application is registered to start at sign-in, which the switch is compared with to tell whether it
    /// was changed.
    /// </summary>
    private bool _autoStartSaved;

    /// <summary>
    /// Names the boss key in the draft.
    /// </summary>
    private const string BossKeyEditKey = "BossKey";

    /// <summary>
    /// The boss key as text, for example <c>Ctrl+Alt+H</c>.
    /// </summary>
    [ObservableProperty]
    private string _bossKeyText = string.Empty;

    /// <summary>
    /// Whether the application starts at sign-in.
    /// </summary>
    [ObservableProperty]
    private bool _autoStart;

    /// <summary>
    /// Whether the application looks for updates by itself.
    /// </summary>
    [ObservableProperty]
    private bool _autoCheckUpdates;

    /// <summary>
    /// A problem with the boss key, or empty.
    /// </summary>
    [ObservableProperty]
    private string _bossKeyError = string.Empty;

    /// <summary>
    /// A problem with the start-at-sign-in switch, or empty.
    /// </summary>
    [ObservableProperty]
    private string _autoStartError = string.Empty;

    /// <summary>
    /// Opens the log directory in the file manager.
    /// </summary>
    [RelayCommand]
    private void OpenLogs()
    {
        _openLogDirectory();
    }

    /// <summary>
    /// Re-reads what can change without the application knowing: whether the application is registered to start at
    /// sign-in (the user can switch that off in Task Manager). A switch the user has changed and not saved is kept.
    /// Call when the tab is shown.
    /// </summary>
    public void RefreshExternalState()
    {
        var enabled = _autoStartService.IsEnabled();
        var untouched = AutoStart == _autoStartSaved;
        _autoStartSaved = enabled;
        if (untouched)
        {
            _refreshing = true;
            try
            {
                AutoStart = enabled;
            }
            finally
            {
                _refreshing = false;
            }
        }

        NotifyUnsavedChanged();
    }

    /// <summary>
    /// Takes the boss key the user entered into the draft. Whether the system accepts it is only known when the settings
    /// are saved.
    /// </summary>
    /// <param name="gesture">
    /// The shortcut the user entered.
    /// </param>
    public void SetBossKey(HotkeyGesture gesture)
    {
        BossKeyError = string.Empty;
        BossKeyText = gesture.ToString();
        if (gesture == _editor.Baseline.BossKey)
        {
            _editor.Clear(BossKeyEditKey);
            return;
        }

        _editor.Set(BossKeyEditKey, settings => settings with { BossKey = gesture });
    }

    /// <summary>
    /// Tells the user that the keys they pressed cannot be used as the boss key.
    /// </summary>
    public void ReportBossKeyRejected()
    {
        BossKeyError = Strings.BossKeyInvalid;
    }

    /// <summary>
    /// Notes that the start-at-sign-in switch was changed; it is applied when the settings are saved.
    /// </summary>
    /// <param name="value">
    /// Whether the application should start at sign-in.
    /// </param>
    partial void OnAutoStartChanged(bool value)
    {
        if (_refreshing)
        {
            return;
        }

        AutoStartError = string.Empty;
        NotifyUnsavedChanged();
    }

    /// <summary>
    /// Applies the choice to look for updates by itself.
    /// </summary>
    /// <param name="value">
    /// Whether the application checks automatically.
    /// </param>
    partial void OnAutoCheckUpdatesChanged(bool value)
    {
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.AutoCheckUpdates), settings => settings with { AutoCheckUpdates = value });
        }
    }

    /// <summary>
    /// Registers or removes the start at sign-in when the switch differs from what is registered. If the system
    /// refuses, the switch goes back to what is registered and the reason is shown.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if there was nothing to change or the change worked.
    /// </returns>
    private bool SaveAutoStart()
    {
        if (AutoStart == _autoStartSaved)
        {
            return true;
        }

        if (_autoStartService.TrySet(AutoStart, out var error))
        {
            _autoStartSaved = AutoStart;
            AutoStartError = string.Empty;
            return true;
        }

        AutoStartError = string.Format(CultureInfo.CurrentCulture, Strings.AutoStartFailed, error);
        _autoStartSaved = _autoStartService.IsEnabled();
        _refreshing = true;
        try
        {
            AutoStart = _autoStartSaved;
        }
        finally
        {
            _refreshing = false;
        }

        return false;
    }

    /// <summary>
    /// Shows the registration problem of the boss key when it appears or goes away on its own, for example when another
    /// program frees the shortcut.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// The event data.
    /// </param>
    private void OnBossKeyStatusChanged(object? sender, EventArgs e)
    {
        if (_applyingBossKey)
        {
            return;
        }

        BossKeyError = _bossKey.RegistrationError is { } reason
            ? string.Format(CultureInfo.CurrentCulture, Strings.BossKeyFailed, _settings.Current.BossKey, reason)
            : string.Empty;
    }
}
