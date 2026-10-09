using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

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
        var count = DeskBuddy.Domain.Chatter.ChatterPolicy.VariantCount;
        return Get($"Chatter{((index % count) + count) % count}");
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
