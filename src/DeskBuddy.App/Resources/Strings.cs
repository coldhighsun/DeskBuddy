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
