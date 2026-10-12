using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Messages;

namespace DeskBuddy.App.Resources;

/// <summary>
/// Typed access to the user-visible text in <c>Strings.resx</c>. All UI wording lives there so it can be localized
/// without touching code. A missing key shows as <c>!Key!</c> instead of crashing.
/// </summary>
internal static class Strings
{
    /// <summary>
    /// Reads the embedded <c>Strings.resx</c> resources.
    /// </summary>
    private static readonly ResourceManager Manager = new("DeskBuddy.App.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// Gets the application name.
    /// </summary>
    public static string AppName => Get();

    /// <summary>
    /// Gets the title of the notice shown after an unhandled error.
    /// </summary>
    public static string CrashTitle => Get();

    /// <summary>
    /// Gets the body of the notice shown after an unhandled error.
    /// </summary>
    public static string CrashMessage => Get();

    /// <summary>
    /// Gets the heading of the diagnostics section.
    /// </summary>
    public static string SettingsDiagnostics => Get();

    /// <summary>
    /// Gets the text of the button that opens the log directory.
    /// </summary>
    public static string OpenLogs => Get();

    /// <summary>
    /// Gets the tray menu text that hides the pet.
    /// </summary>
    public static string TrayHidePet => Get();

    /// <summary>
    /// Gets the tray menu text that shows the pet.
    /// </summary>
    public static string TrayShowPet => Get();

    /// <summary>
    /// Gets the tray menu text that exits the application.
    /// </summary>
    public static string TrayExit => Get();

    /// <summary>
    /// Gets the menu text that opens the panel window.
    /// </summary>
    public static string OpenPanel => Get();

    /// <summary>
    /// Gets the title of the pomodoro tab.
    /// </summary>
    public static string TabPomodoro => Get();

    /// <summary>
    /// Gets the title of the to-do tab.
    /// </summary>
    public static string TabTodo => Get();

    /// <summary>
    /// Gets the title of the note tab.
    /// </summary>
    public static string TabNote => Get();

    /// <summary>
    /// Gets the text showing completed pomodoros; <c>{0}</c> is the count.
    /// </summary>
    public static string PomodoroCompleted => Get();

    /// <summary>
    /// Gets the label above the new to-do box.
    /// </summary>
    public static string TodoNewLabel => Get();

    /// <summary>
    /// Gets the text of the button that adds a to-do.
    /// </summary>
    public static string TodoAdd => Get();

    /// <summary>
    /// Gets the tooltip of the button that deletes a to-do.
    /// </summary>
    public static string TodoDelete => Get();

    /// <summary>
    /// Gets the text of the button that clears completed to-dos.
    /// </summary>
    public static string TodoClearCompleted => Get();

    /// <summary>
    /// Gets the hint shown when the to-do list is empty.
    /// </summary>
    public static string TodoEmpty => Get();

    /// <summary>
    /// Gets the warning shown when the to-do data could not be read; <c>{0}</c> is the reason.
    /// </summary>
    public static string TodoLoadFailed => Get();

    /// <summary>
    /// Gets the warning shown when saving the to-do list failed; <c>{0}</c> is the reason.
    /// </summary>
    public static string TodoSaveFailed => Get();

    /// <summary>
    /// Gets the hint shown above the note box.
    /// </summary>
    public static string NoteHint => Get();

    /// <summary>
    /// Gets the warning shown when the note could not be read; <c>{0}</c> is the reason.
    /// </summary>
    public static string NoteLoadFailed => Get();

    /// <summary>
    /// Gets the warning shown when saving the note failed; <c>{0}</c> is the reason.
    /// </summary>
    public static string NoteSaveFailed => Get();

    /// <summary>
    /// Gets the menu text that starts a pomodoro.
    /// </summary>
    public static string PomodoroStart => Get();

    /// <summary>
    /// Gets the menu text that pauses the pomodoro.
    /// </summary>
    public static string PomodoroPause => Get();

    /// <summary>
    /// Gets the menu text that resumes the pomodoro.
    /// </summary>
    public static string PomodoroResume => Get();

    /// <summary>
    /// Gets the menu text that ends a work phase early.
    /// </summary>
    public static string PomodoroSkipWork => Get();

    /// <summary>
    /// Gets the menu text that skips a break.
    /// </summary>
    public static string PomodoroSkipBreak => Get();

    /// <summary>
    /// Gets the menu text that stops the pomodoro.
    /// </summary>
    public static string PomodoroStop => Get();

    /// <summary>
    /// Gets the status text shown when no pomodoro is running.
    /// </summary>
    public static string StatusStopped => Get();

    /// <summary>
    /// Gets the status text for a work phase; <c>{0}</c> is the remaining minutes.
    /// </summary>
    public static string StatusWork => Get();

    /// <summary>
    /// Gets the status text for a short break; <c>{0}</c> is the remaining minutes.
    /// </summary>
    public static string StatusShortBreak => Get();

    /// <summary>
    /// Gets the status text for a long break; <c>{0}</c> is the remaining minutes.
    /// </summary>
    public static string StatusLongBreak => Get();

    /// <summary>
    /// Gets the status text for a paused phase; <c>{0}</c> is the remaining minutes.
    /// </summary>
    public static string StatusPaused => Get();

    /// <summary>
    /// Gets the bubble text for a finished work phase; <c>{0}</c> is the break minutes, <c>{1}</c> the number of
    /// completed pomodoros.
    /// </summary>
    public static string MessageWorkCompleted => Get();

    /// <summary>
    /// Gets the bubble text for a finished break.
    /// </summary>
    public static string MessageBreakCompleted => Get();

    /// <summary>
    /// Gets the bubble text reminding the user to take a break; <c>{0}</c> is how many minutes they have worked.
    /// </summary>
    public static string MessageSedentary => Get();

    /// <summary>
    /// Gets the bubble button text that closes the bubble.
    /// </summary>
    public static string ActionDismiss => Get();

    /// <summary>
    /// Gets the bubble button text that postpones the reminder.
    /// </summary>
    public static string ActionSnoozeReminder => Get();

    /// <summary>
    /// Gets the bubble button text that skips the break.
    /// </summary>
    public static string ActionSkipBreak => Get();

    /// <summary>
    /// Gets the bubble button text that starts the next round.
    /// </summary>
    public static string ActionStartPomodoro => Get();

    /// <summary>
    /// Gets the title of the settings tab.
    /// </summary>
    public static string TabSettings => Get();

    /// <summary>
    /// Gets the menu text that opens the settings tab.
    /// </summary>
    public static string MenuSettings => Get();

    /// <summary>
    /// Gets the heading of the appearance section.
    /// </summary>
    public static string SettingsAppearance => Get();

    /// <summary>
    /// Gets the label of the skin choice.
    /// </summary>
    public static string SettingsSkin => Get();

    /// <summary>
    /// Gets the label of the disturbance mode choice.
    /// </summary>
    public static string SettingsMode => Get();

    /// <summary>
    /// Gets the text of the docked mode.
    /// </summary>
    public static string ModeDocked => Get();

    /// <summary>
    /// Gets the text of the roaming mode.
    /// </summary>
    public static string ModeRoam => Get();

    /// <summary>
    /// Gets the heading of the timing section.
    /// </summary>
    public static string SettingsTiming => Get();

    /// <summary>
    /// Gets the label of the pomodoro work length.
    /// </summary>
    public static string SettingsPomodoroWork => Get();

    /// <summary>
    /// Gets the label of the short break length.
    /// </summary>
    public static string SettingsPomodoroShortBreak => Get();

    /// <summary>
    /// Gets the label of the long break length.
    /// </summary>
    public static string SettingsPomodoroLongBreak => Get();

    /// <summary>
    /// Gets the label of the sedentary reminder interval.
    /// </summary>
    public static string SettingsSedentary => Get();

    /// <summary>
    /// Gets the label of the time before the pet falls asleep.
    /// </summary>
    public static string SettingsSleep => Get();

    /// <summary>
    /// Gets the heading of the boss key section.
    /// </summary>
    public static string SettingsBossKey => Get();

    /// <summary>
    /// Gets the hint for entering the boss key.
    /// </summary>
    public static string BossKeyHint => Get();

    /// <summary>
    /// Gets the message for a boss key that could not be registered; <c>{0}</c> is the shortcut, <c>{1}</c> the reason.
    /// </summary>
    public static string BossKeyFailed => Get();

    /// <summary>
    /// Gets the message for keys that cannot be a boss key.
    /// </summary>
    public static string BossKeyInvalid => Get();

    /// <summary>
    /// Gets the heading of the full-screen section.
    /// </summary>
    public static string SettingsFullscreen => Get();

    /// <summary>
    /// Gets the text of the auto-hide switch.
    /// </summary>
    public static string AutoHide => Get();

    /// <summary>
    /// Gets the text of the monitor-cover switch.
    /// </summary>
    public static string CoveredMonitor => Get();

    /// <summary>
    /// Gets the label of the process list.
    /// </summary>
    public static string BlacklistLabel => Get();

    /// <summary>
    /// Gets the heading of the development section.
    /// </summary>
    public static string SettingsDev => Get();

    /// <summary>
    /// Gets the text of the development detection switch.
    /// </summary>
    public static string DevMode => Get();

    /// <summary>
    /// Gets the label of the development tool list.
    /// </summary>
    public static string DevProcessesLabel => Get();

    /// <summary>
    /// Gets the heading of the system section.
    /// </summary>
    public static string SettingsSystem => Get();

    /// <summary>
    /// Gets the text of the start-at-sign-in switch.
    /// </summary>
    public static string AutoStart => Get();

    /// <summary>
    /// Gets the message for a failed start-at-sign-in change; <c>{0}</c> is the reason.
    /// </summary>
    public static string AutoStartFailed => Get();

    /// <summary>
    /// Gets the message for a minutes value out of range; <c>{0}</c> is the entry, <c>{1}</c> and <c>{2}</c> the range.
    /// </summary>
    public static string MinutesRange => Get();

    /// <summary>
    /// Gets the message for a failed save; <c>{0}</c> is the reason.
    /// </summary>
    public static string SettingsSaveFailed => Get();

    /// <summary>
    /// Gets the message for settings that could not be read; <c>{0}</c> is the reason.
    /// </summary>
    public static string SettingsLoadFailed => Get();

    /// <summary>
    /// Gets the message for a skin that could not be switched to; <c>{0}</c> is the reason.
    /// </summary>
    public static string SkinSwitchFailed => Get();

    /// <summary>
    /// Gets one of the pet's casual remarks.
    /// </summary>
    /// <param name="index">
    /// Which remark, from 0 to <see cref="DeskBuddy.Domain.Chatter.ChatterPolicy.VariantCount"/> - 1; larger values
    /// wrap around.
    /// </param>
    /// <returns>
    /// The localized remark.
    /// </returns>
    public static string Chatter(int index)
    {
        var count = Domain.Chatter.ChatterPolicy.VariantCount;
        return Get($"Chatter{((index % count) + count) % count}");
    }

    /// <summary>
    /// Gets the heading of the menu that previews the pet's animations.
    /// </summary>
    public static string MenuPreview => Get();

    /// <summary>
    /// Gets the label of the language setting.
    /// </summary>
    public static string SettingsLanguage => Get();

    /// <summary>
    /// Gets the label of the pet size setting.
    /// </summary>
    public static string SettingsPetSize => Get();

    /// <summary>
    /// Gets the name of the small pet size.
    /// </summary>
    public static string PetSizeSmall => Get();

    /// <summary>
    /// Gets the name of the medium pet size.
    /// </summary>
    public static string PetSizeMedium => Get();

    /// <summary>
    /// Gets the name of the large pet size.
    /// </summary>
    public static string PetSizeLarge => Get();

    /// <summary>
    /// Gets the name of the choice that follows the system language.
    /// </summary>
    public static string LanguageAuto => Get();

    /// <summary>
    /// Gets the name of the Simplified Chinese choice.
    /// </summary>
    public static string LanguageZhCN => Get();

    /// <summary>
    /// Gets the name of the English choice.
    /// </summary>
    public static string LanguageEn => Get();

    /// <summary>
    /// Gets the note that a language change needs a restart.
    /// </summary>
    public static string LanguageRestartHint => Get();

    /// <summary>
    /// Gets the short name of a state, for menus.
    /// </summary>
    /// <param name="state">
    /// The state.
    /// </param>
    /// <returns>
    /// The localized name, or the name of the enum value if there is none.
    /// </returns>
    public static string StateName(PetState state)
    {
        return Manager.GetString($"StateName{state}", CultureInfo.CurrentUICulture) ?? state.ToString();
    }

    /// <summary>
    /// Gets the short explanation of what the pet is doing in a state, shown when the mouse rests on it.
    /// </summary>
    /// <param name="state">
    /// The state the pet is in.
    /// </param>
    /// <returns>
    /// The localized explanation, or <see langword="null"/> if the state needs none.
    /// </returns>
    public static string? StateHint(PetState state)
    {
        return Manager.GetString($"StateHint{state}", CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Gets the heading of the scenarios section.
    /// </summary>
    public static string SettingsScenarios => Get();

    /// <summary>
    /// Gets the label of the meeting application list.
    /// </summary>
    public static string MeetingProcessesLabel => Get();

    /// <summary>
    /// Gets the label of the box for the time the working day ends.
    /// </summary>
    public static string ClockOutTimeLabel => Get();

    /// <summary>
    /// Gets the explanation shown when the clock-out time is not a time of day.
    /// </summary>
    public static string ClockOutTimeInvalid => Get();

    /// <summary>
    /// Gets the text of the switch for all scenarios.
    /// </summary>
    public static string ScenariosEnabled => Get();

    /// <summary>
    /// Gets the text of the switch for one scenario.
    /// </summary>
    /// <param name="state">
    /// The scenario, identified by the state the pet shows for it.
    /// </param>
    /// <returns>
    /// The localized text.
    /// </returns>
    public static string ScenarioLabel(PetState state)
    {
        return Manager.GetString($"Scenario{state}", CultureInfo.CurrentUICulture) ?? $"!Scenario{state}!";
    }

    /// <summary>
    /// Gets the short remark that goes with a state the pet acts out.
    /// </summary>
    /// <param name="code">
    /// The state the remark belongs to and its wording, packed by <see cref="RemarkCode"/>.
    /// </param>
    /// <returns>
    /// The localized remark, or an empty string if that state has none.
    /// </returns>
    public static string Remark(int code)
    {
        var (state, variant) = RemarkCode.Decode(code);
        var text = (variant > 0 ? Manager.GetString($"Remark{state}{variant}", CultureInfo.CurrentUICulture) : null)
            ?? Manager.GetString($"Remark{state}", CultureInfo.CurrentUICulture)
            ?? string.Empty;
        return state == PetState.Chime ? string.Format(CultureInfo.CurrentCulture, text, variant) : text;
    }

    /// <summary>
    /// Looks up a string by the name of the calling property.
    /// </summary>
    /// <param name="key">
    /// The resource key; filled in automatically with the caller's name.
    /// </param>
    /// <returns>
    /// The localized text for the current UI culture, or <c>!key!</c> if it is missing.
    /// </returns>
    private static string Get([CallerMemberName] string key = "")
    {
        return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";
    }
}
