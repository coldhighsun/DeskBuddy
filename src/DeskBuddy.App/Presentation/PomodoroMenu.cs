using System.Globalization;
using System.Windows.Controls;
using DeskBuddy.App.Resources;
using DeskBuddy.Application;
using DeskBuddy.Domain.Pomodoro;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Builds the pomodoro part of the pet's context menu and the tray menu, and the status text shown with it.
/// </summary>
public static class PomodoroMenu
{
    /// <summary>
    /// Formats the one-line pomodoro status, for example "专注中，剩余 18 分钟".
    /// </summary>
    /// <param name="session">
    /// The pomodoro timer.
    /// </param>
    /// <returns>
    /// The localized status text.
    /// </returns>
    public static string FormatStatus(PomodoroSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var minutes = MessagePresenter.WholeMinutes(session.Remaining);
        var format = session switch
        {
            { Phase: PomodoroPhase.Stopped } => Strings.StatusStopped,
            { IsPaused: true } => Strings.StatusPaused,
            { Phase: PomodoroPhase.Work } => Strings.StatusWork,
            { Phase: PomodoroPhase.ShortBreak } => Strings.StatusShortBreak,
            _ => Strings.StatusLongBreak,
        };

        return string.Format(CultureInfo.CurrentCulture, format, minutes);
    }

    /// <summary>
    /// Replaces the contents of a menu with a status line and the pomodoro commands that make sense right now.
    /// </summary>
    /// <param name="items">
    /// The menu's items; cleared first.
    /// </param>
    /// <param name="brain">
    /// The brain whose pomodoro is controlled.
    /// </param>
    /// <param name="afterCommand">
    /// Called after any command so the pet can react immediately.
    /// </param>
    public static void Populate(ItemCollection items, PetBrain brain, Action afterCommand)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(afterCommand);

        var session = brain.Pomodoro;
        items.Clear();
        items.Add(new MenuItem { Header = FormatStatus(session), IsEnabled = false });
        items.Add(new Separator());

        if (session.Phase == PomodoroPhase.Stopped)
        {
            items.Add(Command(Strings.PomodoroStart, brain.StartPomodoro, afterCommand));
            return;
        }

        items.Add(session.IsPaused
            ? Command(Strings.PomodoroResume, brain.ResumePomodoro, afterCommand)
            : Command(Strings.PomodoroPause, brain.PausePomodoro, afterCommand));
        items.Add(Command(
            session.Phase == PomodoroPhase.Work ? Strings.PomodoroSkipWork : Strings.PomodoroSkipBreak,
            brain.SkipPomodoroPhase,
            afterCommand));
        items.Add(Command(Strings.PomodoroStop, brain.StopPomodoro, afterCommand));
    }

    /// <summary>
    /// Creates a menu item that runs a command and then the follow-up callback.
    /// </summary>
    /// <param name="header">
    /// The menu text.
    /// </param>
    /// <param name="command">
    /// The command to run.
    /// </param>
    /// <param name="afterCommand">
    /// Called after the command.
    /// </param>
    /// <returns>
    /// The menu item.
    /// </returns>
    private static MenuItem Command(string header, Action command, Action afterCommand)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            command();
            afterCommand();
        };
        return item;
    }
}
