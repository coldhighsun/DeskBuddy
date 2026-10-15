using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Settings;
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
    /// Gets the tray menu text of the submenu that switches the pet.
    /// </summary>
    public static string TraySwitchPet => Get();

    /// <summary>
    /// Gets the tray menu text of the submenu that changes the pet size.
    /// </summary>
    public static string TrayPetSize => Get();

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
    /// Gets the title of the pets tab.
    /// </summary>
    public static string TabPets => Get();

    /// <summary>
    /// Gets the badge on the pet in use.
    /// </summary>
    public static string PetsCurrent => Get();

    /// <summary>
    /// Gets the text of the button that switches to a pet.
    /// </summary>
    public static string PetsUse => Get();

    /// <summary>
    /// Gets the line about a pet's picture size and states; <c>{0}</c> and <c>{1}</c> are its width and height, <c>{2}</c>
    /// the number of states it draws itself and <c>{3}</c> the number of states there are.
    /// </summary>
    public static string PetsInfo => Get();

    /// <summary>
    /// Gets the label of a pet whose eyes follow the mouse.
    /// </summary>
    public static string PetsCapLookAround => Get();

    /// <summary>
    /// Gets the label for climbing the screen edges; <c>{0}</c> of <c>{1}</c> states are drawn.
    /// </summary>
    public static string PetsCapEdges => Get();

    /// <summary>
    /// Gets the label for keeping company while developing; <c>{0}</c> of <c>{1}</c> states are drawn.
    /// </summary>
    public static string PetsCapDevelopment => Get();

    /// <summary>
    /// Gets the label for everyday situations; <c>{0}</c> of <c>{1}</c> states are drawn.
    /// </summary>
    public static string PetsCapScenarios => Get();

    /// <summary>
    /// Gets the message for a pet that could not be loaded; <c>{0}</c> is the reason.
    /// </summary>
    public static string PetsLoadFailed => Get();

    /// <summary>
    /// Gets the message shown when no pet is installed.
    /// </summary>
    public static string PetsEmpty => Get();

    /// <summary>
    /// Gets the name of a state, shown under the pet preview.
    /// </summary>
    /// <param name="state">
    /// The state being previewed.
    /// </param>
    /// <returns>
    /// The localized name.
    /// </returns>
    public static string PetStateName(PetState state)
    {
        return Manager.GetString($"PetStateName{state}", CultureInfo.CurrentUICulture) ?? $"!PetStateName{state}!";
    }

    /// <summary>
    /// Gets the title of the appearance tab.
    /// </summary>
    public static string TabAppearance => Get();

    /// <summary>
    /// Gets the title of the behavior tab.
    /// </summary>
    public static string TabBehavior => Get();

    /// <summary>
    /// Gets the title of the system tab.
    /// </summary>
    public static string TabSystem => Get();

    /// <summary>
    /// Gets the heading of the appearance section.
    /// </summary>
    public static string SettingsAppearance => Get();

    /// <summary>
    /// Gets the heading of the timing section.
    /// </summary>
    public static string SettingsTiming => Get();

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
    /// Gets the heading of the section with the lists of programs the pet recognizes.
    /// </summary>
    public static string SettingsPrograms => Get();

    /// <summary>
    /// Gets the hint under the heading of the program lists.
    /// </summary>
    public static string ProgramsHint => Get();

    /// <summary>
    /// Gets the heading of the section with what the pet does on its own.
    /// </summary>
    public static string SettingsActions => Get();

    /// <summary>
    /// Gets the note that some actions need a pet with its own animations.
    /// </summary>
    public static string PetSupportNote => Get();

    /// <summary>
    /// Gets the heading of the note about process names taken out of the lists when the settings were loaded.
    /// </summary>
    public static string ProcessesRemovedNotice => Get();

    /// <summary>
    /// Gets the line for a process name taken out for having a space, with the name as <c>{0}</c> and the list as
    /// <c>{1}</c>.
    /// </summary>
    public static string ProcessRemovedHasSpace => Get();

    /// <summary>
    /// Gets the line for a process name taken out of a list because it is in another one, with the name as <c>{0}</c>,
    /// the list it was taken out of as <c>{1}</c> and the list it stays in as <c>{2}</c>.
    /// </summary>
    public static string ProcessRemovedInOtherList => Get();

    /// <summary>
    /// Gets the explanation for a process name with a space in it, with the name as <c>{0}</c>.
    /// </summary>
    public static string ProcessNameHasSpace => Get();

    /// <summary>
    /// Gets the explanation for a process name that is in another list, with the name as <c>{0}</c> and the list as
    /// <c>{1}</c>.
    /// </summary>
    public static string ProcessNameInOtherList => Get();

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
    /// Gets the message for a pet that could not be switched to; <c>{0}</c> is the reason.
    /// </summary>
    public static string PetSwitchFailed => Get();

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
    /// Gets the label of the language setting.
    /// </summary>
    public static string SettingsLanguage => Get();

    /// <summary>
    /// Gets the label of the theme setting.
    /// </summary>
    public static string SettingsTheme => Get();

    /// <summary>
    /// Gets the label of the pet size setting.
    /// </summary>
    public static string SettingsPetSize => Get();

    /// <summary>
    /// Gets the label of the setting that lets the pet climb the edges of the screen.
    /// </summary>
    public static string SettingsClimbEdges => Get();

    /// <summary>
    /// Gets the label of the setting that lets the pet say something unprompted now and then.
    /// </summary>
    public static string SettingsChatter => Get();

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
    /// Gets the name of the choice that follows the system theme.
    /// </summary>
    public static string ThemeAuto => Get();

    /// <summary>
    /// Gets the name of the light theme choice.
    /// </summary>
    public static string ThemeLight => Get();

    /// <summary>
    /// Gets the name of the dark theme choice.
    /// </summary>
    public static string ThemeDark => Get();

    /// <summary>
    /// Gets the note that a language change needs a restart.
    /// </summary>
    public static string LanguageRestartHint => Get();

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
    /// Gets the label of the list of chat applications.
    /// </summary>
    public static string ChatProcessesLabel => Get();

    /// <summary>
    /// Gets the label of the box for the time the working day ends.
    /// </summary>
    public static string ClockOutTimeLabel => Get();

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
    /// Gets the name of a process list, for saying where a process already is.
    /// </summary>
    /// <param name="kind">
    /// The list.
    /// </param>
    /// <returns>
    /// The localized name.
    /// </returns>
    public static string ProcessListName(ProcessListKind kind)
    {
        return Manager.GetString($"ProcessList{kind}", CultureInfo.CurrentUICulture) ?? $"!ProcessList{kind}!";
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
