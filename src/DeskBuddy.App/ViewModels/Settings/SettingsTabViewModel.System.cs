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
    /// sign-in (the user can switch that off in Task Manager). Call when the tab is shown.
    /// </summary>
    public void RefreshExternalState()
    {
        _refreshing = true;
        try
        {
            AutoStart = _autoStartService.IsEnabled();
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Tries a new boss key. If the system refuses it (another program uses it) the setting goes back to the shortcut
    /// that works, so the settings always name the shortcut that is active, and the reason is shown.
    /// </summary>
    /// <param name="gesture">
    /// The shortcut the user entered.
    /// </param>
    /// <returns>
    /// A task that completes when the shortcut has been applied or rejected.
    /// </returns>
    public async Task ApplyBossKeyAsync(HotkeyGesture gesture)
    {
        var previous = _settings.Current.BossKey;
        if (gesture == previous)
        {
            BossKeyError = string.Empty;
            return;
        }

        _applyingBossKey = true;
        try
        {
            await ApplyAsync(settings => settings with { BossKey = gesture });
            if (_bossKey.RegistrationError is { } reason)
            {
                var working = _bossKey.Registered ?? previous;
                await ApplyAsync(settings => settings with { BossKey = working });
                BossKeyError = string.Format(CultureInfo.CurrentCulture, Strings.BossKeyFailed, gesture, reason);
            }
            else
            {
                BossKeyError = string.Empty;
            }
        }
        finally
        {
            _applyingBossKey = false;
        }
    }

    /// <summary>
    /// Tells the user that the keys they pressed cannot be used as the boss key.
    /// </summary>
    public void ReportBossKeyRejected()
    {
        BossKeyError = Strings.BossKeyInvalid;
    }

    /// <summary>
    /// Switches start at sign-in on or off. If the system does not allow it the switch goes back and the reason is
    /// shown.
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

        if (_autoStartService.TrySet(value, out var error))
        {
            AutoStartError = string.Empty;
            return;
        }

        AutoStartError = string.Format(CultureInfo.CurrentCulture, Strings.AutoStartFailed, error);
        _refreshing = true;
        try
        {
            AutoStart = _autoStartService.IsEnabled();
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Shows a boss key that could not be registered when that happens outside an entry by the user.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
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
