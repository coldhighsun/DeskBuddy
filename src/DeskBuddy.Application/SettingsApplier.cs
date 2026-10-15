using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application;

/// <summary>
/// Copies the user's settings into the shared option objects the behavior, sedentary, visibility, chatter and
/// development-detection logic read from, at
/// startup and after every change. Those classes read their options each time they need a value, so a change takes
/// effect without a restart; what is already running keeps its schedule and the new value applies from the next one.
/// </summary>
public sealed class SettingsApplier
{
    /// <summary>
    /// The behavior options to update.
    /// </summary>
    private readonly PetBehaviorOptions _behavior;

    /// <summary>
    /// The sedentary options to update.
    /// </summary>
    private readonly SedentaryOptions _sedentary;

    /// <summary>
    /// The visibility rules to update.
    /// </summary>
    private readonly PetVisibility _visibility;

    /// <summary>
    /// The policy for unprompted remarks to update.
    /// </summary>
    private readonly ChatterPolicy _chatter;

    /// <summary>
    /// The development detection to update.
    /// </summary>
    private readonly DevActivityPolicy _devActivity;

    /// <summary>
    /// The scenario switches to update.
    /// </summary>
    private readonly ScenarioSwitches _scenarios;

    /// <summary>
    /// The meeting detection to update.
    /// </summary>
    private readonly MeetingPolicy _meeting;

    /// <summary>
    /// The chat detection to update.
    /// </summary>
    private readonly ChatPolicy _chat;

    /// <summary>
    /// Runs after the settings were copied, to put back values that are forced from outside (such as configuration
    /// overrides used for diagnostics); <see langword="null"/> if there are none.
    /// </summary>
    private readonly Action? _reapplyOverrides;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsApplier"/> class and applies the current settings.
    /// </summary>
    /// <param name="settings">
    /// The settings to follow.
    /// </param>
    /// <param name="behavior">
    /// The behavior options to update.
    /// </param>
    /// <param name="sedentary">
    /// The sedentary options to update.
    /// </param>
    /// <param name="visibility">
    /// The visibility rules to update.
    /// </param>
    /// <param name="chatter">
    /// The policy for unprompted remarks to update.
    /// </param>
    /// <param name="devActivity">
    /// The development detection to update.
    /// </param>
    /// <param name="scenarios">
    /// The scenario switches to update.
    /// </param>
    /// <param name="meeting">
    /// The meeting detection to update.
    /// </param>
    /// <param name="chat">
    /// The chat detection to update.
    /// </param>
    /// <param name="reapplyOverrides">
    /// Runs after each application of the settings; <see langword="null"/> if nothing overrides them.
    /// </param>
    public SettingsApplier(
        SettingsService settings,
        PetBehaviorOptions behavior,
        SedentaryOptions sedentary,
        PetVisibility visibility,
        ChatterPolicy chatter,
        DevActivityPolicy devActivity,
        ScenarioSwitches scenarios,
        MeetingPolicy meeting,
        ChatPolicy chat,
        Action? reapplyOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(behavior);
        ArgumentNullException.ThrowIfNull(sedentary);
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentNullException.ThrowIfNull(chatter);
        ArgumentNullException.ThrowIfNull(devActivity);
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentNullException.ThrowIfNull(chat);

        _behavior = behavior;
        _sedentary = sedentary;
        _visibility = visibility;
        _chatter = chatter;
        _devActivity = devActivity;
        _scenarios = scenarios;
        _meeting = meeting;
        _chat = chat;
        _reapplyOverrides = reapplyOverrides;

        Apply(settings.Current);
        settings.Changed += (_, current) => Apply(current);
    }

    /// <summary>
    /// Copies the settings into the option objects.
    /// </summary>
    /// <param name="settings">
    /// The settings to copy.
    /// </param>
    private void Apply(PetSettings settings)
    {
        _behavior.SleepAfter = settings.SleepAfter;
        _sedentary.ReminderAfter = settings.SedentaryReminderAfter;
        _visibility.AutoHideEnabled = settings.AutoHideWhenFullscreen;
        _chatter.IsEnabled = settings.ChatterEnabled;
        _devActivity.Processes = settings.DevProcesses;
        _scenarios.IsEnabled = settings.ScenariosEnabled;
        _scenarios.Disabled = settings.DisabledScenarios;
        _meeting.Processes = settings.MeetingProcesses;
        _chat.Processes = settings.ChatProcesses;
        _behavior.ClockOutTime = settings.ClockOutTime;

        _reapplyOverrides?.Invoke();
    }
}
