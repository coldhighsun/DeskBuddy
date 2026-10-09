using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Pomodoro;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;

namespace DeskBuddy.Application;

/// <summary>
/// Gathers sensor input, the pomodoro timer and user interactions and drives the pet's behavior. This is the single
/// place the UI talks to for "what should the pet be doing" and "is there something to tell the user".
/// </summary>
/// <param name="idleDetector">
/// Reports how long the user has been inactive.
/// </param>
/// <param name="loadMonitor">
/// Reports how busy the machine is.
/// </param>
/// <param name="loadPolicy">
/// Smooths load readings into a stable "strained" flag.
/// </param>
/// <param name="fullscreenDetector">
/// Tells when the user must not be disturbed, so messages are held back.
/// </param>
/// <param name="pomodoro">
/// The pomodoro timer.
/// </param>
/// <param name="sedentary">
/// Decides when to remind the user to get up.
/// </param>
/// <param name="messages">
/// The queue of messages for the speech bubble.
/// </param>
/// <param name="stateMachine">
/// The behavior state machine to drive.
/// </param>
public sealed class PetBrain(
    IIdleDetector idleDetector,
    ISystemLoadMonitor loadMonitor,
    LoadReactionPolicy loadPolicy,
    IFullscreenDetector fullscreenDetector,
    PomodoroSession pomodoro,
    SedentaryReminderPolicy sedentary,
    PetMessageQueue messages,
    PetBehaviorStateMachine stateMachine)
{
    /// <summary>
    /// The pomodoro status last announced through <see cref="PomodoroStatusChanged"/>, to detect changes. The
    /// remaining time is rounded up to whole minutes, so the event fires about once a minute while a phase runs.
    /// </summary>
    private (PomodoroPhase Phase, bool IsPaused, int MinutesLeft) _announcedStatus;

    /// <summary>
    /// Raised when the message on screen appears, changes or goes away. Read <see cref="CurrentMessage"/>.
    /// </summary>
    public event EventHandler? MessageChanged;

    /// <summary>
    /// Raised when the pomodoro phase, paused flag or remaining whole minutes change.
    /// </summary>
    public event EventHandler? PomodoroStatusChanged;

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public PetState State => stateMachine.State;

    /// <summary>
    /// Gets the direction of the current or most recent walk: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    public int WalkDirection => stateMachine.WalkDirection;

    /// <summary>
    /// Gets the pomodoro timer, for display. Change it through the brain's commands so the pet reacts.
    /// </summary>
    public PomodoroSession Pomodoro => pomodoro;

    /// <summary>
    /// Gets the message to show in the speech bubble, or <see langword="null"/> if none.
    /// </summary>
    public PetMessage? CurrentMessage => messages.Current;

    /// <summary>
    /// Reads the sensors and advances the timer, message queue and state machine. Call periodically, and after any
    /// command so the result shows immediately.
    /// </summary>
    /// <returns>
    /// The state after the update.
    /// </returns>
    public PetState Tick()
    {
        var userIdle = idleDetector.GetIdleTime();
        var isStrained = loadPolicy.Update(loadMonitor.GetSnapshot());

        EnqueueMessageFor(pomodoro.Update());

        // A running pomodoro already paces the user's breaks, so it replaces the sedentary reminder.
        if (sedentary.Update(userIdle, isCoveredElsewhere: pomodoro.Phase != PomodoroPhase.Stopped))
        {
            messages.Enqueue(new PetMessage(
                PetMessageKind.SedentaryReminder,
                [MessageAction.SnoozeReminder, MessageAction.Dismiss],
                sedentary.StreakDuration));
        }

        if (messages.Update(fullscreenDetector.IsDoNotDisturb()))
        {
            MessageChanged?.Invoke(this, EventArgs.Empty);
        }

        AnnouncePomodoroStatusIfChanged();

        var inputs = new PetInputs(
            userIdle,
            isStrained,
            IsFocusing: pomodoro.IsRunning && pomodoro.Phase == PomodoroPhase.Work,
            HasAlert: messages.Current is not null);
        return stateMachine.Update(inputs);
    }

    /// <summary>
    /// Starts a pomodoro work phase and closes any "start the next round" prompt.
    /// </summary>
    public void StartPomodoro()
    {
        pomodoro.Start();
        DismissMessage();
    }

    /// <summary>
    /// Pauses the running pomodoro phase.
    /// </summary>
    public void PausePomodoro()
    {
        pomodoro.Pause();
    }

    /// <summary>
    /// Resumes a paused pomodoro phase.
    /// </summary>
    public void ResumePomodoro()
    {
        pomodoro.Resume();
    }

    /// <summary>
    /// Ends the current pomodoro phase early (see <see cref="PomodoroSession.Skip"/>).
    /// </summary>
    public void SkipPomodoroPhase()
    {
        pomodoro.Skip();
    }

    /// <summary>
    /// Stops the pomodoro and discards its progress.
    /// </summary>
    public void StopPomodoro()
    {
        pomodoro.Stop();
    }

    /// <summary>
    /// Carries out an action chosen from a speech bubble, then closes the bubble.
    /// </summary>
    /// <param name="action">
    /// The chosen action.
    /// </param>
    public void Invoke(MessageAction action)
    {
        switch (action)
        {
            case MessageAction.SkipBreak:
                pomodoro.Skip();
                break;
            case MessageAction.StartPomodoro:
                pomodoro.Start();
                break;
            case MessageAction.SnoozeReminder:
                sedentary.Snooze();
                break;
        }

        DismissMessage();
    }

    /// <summary>
    /// Closes the message on screen, if any.
    /// </summary>
    public void DismissMessage()
    {
        if (messages.Dismiss())
        {
            MessageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Notifies the brain that the user started dragging the pet.
    /// </summary>
    public void OnDragStarted()
    {
        stateMachine.OnDragStarted();
    }

    /// <summary>
    /// Notifies the brain that the user dropped the pet.
    /// </summary>
    public void OnDragEnded()
    {
        stateMachine.OnDragEnded();
    }

    /// <summary>
    /// Notifies the brain that the user clicked the pet. Counts as acknowledging the message on screen.
    /// </summary>
    public void OnClicked()
    {
        DismissMessage();
        stateMachine.OnClicked();
    }

    /// <summary>
    /// Queues the reminder that belongs to a pomodoro phase boundary. Stale boundaries (the machine was asleep) are
    /// ignored.
    /// </summary>
    /// <param name="transition">
    /// The boundary the timer just crossed.
    /// </param>
    private void EnqueueMessageFor(PomodoroTransition transition)
    {
        if (transition.IsStale)
        {
            return;
        }

        switch (transition.Kind)
        {
            case PomodoroTransitionKind.WorkCompleted:
                messages.Enqueue(new PetMessage(
                    PetMessageKind.PomodoroWorkCompleted,
                    [MessageAction.SkipBreak, MessageAction.Dismiss],
                    pomodoro.Remaining,
                    pomodoro.CompletedWorkSessions));
                break;
            case PomodoroTransitionKind.BreakCompleted:
                messages.Enqueue(new PetMessage(
                    PetMessageKind.PomodoroBreakCompleted,
                    [MessageAction.StartPomodoro, MessageAction.Dismiss]));
                break;
        }
    }

    /// <summary>
    /// Raises <see cref="PomodoroStatusChanged"/> if the visible pomodoro status differs from the last announcement.
    /// </summary>
    private void AnnouncePomodoroStatusIfChanged()
    {
        var status = (pomodoro.Phase, pomodoro.IsPaused, (int)Math.Ceiling(pomodoro.Remaining.TotalMinutes));
        if (status == _announcedStatus)
        {
            return;
        }

        _announcedStatus = status;
        PomodoroStatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
