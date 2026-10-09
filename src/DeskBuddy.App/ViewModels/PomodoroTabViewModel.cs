using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Resources;
using DeskBuddy.Application;
using DeskBuddy.Domain.Pomodoro;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// Backs the pomodoro tab: a countdown with start, pause, skip and stop. Contains no logic of its own; every command
/// goes through <see cref="PetBrain"/> so the pet reacts exactly as it does to the menu commands.
/// </summary>
public sealed partial class PomodoroTabViewModel : ObservableObject
{
    /// <summary>
    /// The brain whose pomodoro is shown and controlled.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Called after any command so the pet reacts immediately.
    /// </summary>
    private readonly Action _afterCommand;

    /// <summary>
    /// The one-line status, for example "专注中，剩余 18 分钟".
    /// </summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// The remaining time as <c>mm:ss</c>.
    /// </summary>
    [ObservableProperty]
    private string _remainingText = "00:00";

    /// <summary>
    /// The label of the main button: start, pause or resume.
    /// </summary>
    [ObservableProperty]
    private string _primaryLabel = string.Empty;

    /// <summary>
    /// The label of the skip button: end the work phase or skip the break.
    /// </summary>
    [ObservableProperty]
    private string _skipLabel = string.Empty;

    /// <summary>
    /// Whether a phase is active, which is when skip and stop make sense.
    /// </summary>
    [ObservableProperty]
    private bool _hasActivePhase;

    /// <summary>
    /// The text showing how many pomodoros the current cycle has completed.
    /// </summary>
    [ObservableProperty]
    private string _completedText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PomodoroTabViewModel"/> class.
    /// </summary>
    /// <param name="brain">
    /// The brain whose pomodoro is shown and controlled.
    /// </param>
    /// <param name="afterCommand">
    /// Called after any command so the pet reacts immediately.
    /// </param>
    public PomodoroTabViewModel(PetBrain brain, Action afterCommand)
    {
        _brain = brain;
        _afterCommand = afterCommand;
        Refresh();
    }

    /// <summary>
    /// Re-reads the pomodoro state into the displayed properties. Call about once a second while the tab is visible.
    /// </summary>
    public void Refresh()
    {
        var session = _brain.Pomodoro;
        var remaining = session.Remaining;

        StatusText = PomodoroMenu.FormatStatus(session);
        RemainingText = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        HasActivePhase = session.Phase != PomodoroPhase.Stopped;
        PrimaryLabel = session switch
        {
            { Phase: PomodoroPhase.Stopped } => Strings.PomodoroStart,
            { IsPaused: true } => Strings.PomodoroResume,
            _ => Strings.PomodoroPause,
        };
        SkipLabel = session.Phase == PomodoroPhase.Work ? Strings.PomodoroSkipWork : Strings.PomodoroSkipBreak;
        CompletedText = string.Format(CultureInfo.CurrentCulture, Strings.PomodoroCompleted, session.CompletedWorkSessions);
    }

    /// <summary>
    /// Starts, pauses or resumes, depending on the current state.
    /// </summary>
    [RelayCommand]
    private void Primary()
    {
        var session = _brain.Pomodoro;
        if (session.Phase == PomodoroPhase.Stopped)
        {
            _brain.StartPomodoro();
        }
        else if (session.IsPaused)
        {
            _brain.ResumePomodoro();
        }
        else
        {
            _brain.PausePomodoro();
        }

        Finish();
    }

    /// <summary>
    /// Ends the current phase early.
    /// </summary>
    [RelayCommand]
    private void Skip()
    {
        _brain.SkipPomodoroPhase();
        Finish();
    }

    /// <summary>
    /// Stops the pomodoro and discards its progress.
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        _brain.StopPomodoro();
        Finish();
    }

    /// <summary>
    /// Lets the pet react to the command and refreshes the display.
    /// </summary>
    private void Finish()
    {
        _afterCommand();
        Refresh();
    }
}
