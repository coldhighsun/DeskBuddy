using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application;

/// <summary>
/// Gathers sensor input and user interactions and drives the pet's behavior. This is the single
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
/// Tells when the user must not be disturbed, so messages are held back and the pet may hide.
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
/// <param name="visibility">
/// Decides whether the pet is on screen.
/// </param>
/// <param name="chatter">
/// Decides when the pet says something unprompted.
/// </param>
/// <param name="devScenarios">
/// Works out what the pet should do because of the user's development work: sitting down to code, builds, git, long
/// nights, reading up.
/// </param>
/// <param name="systemScenarios">
/// Works out what the pet should do because of things around the user: a meeting, coming back, the battery.
/// </param>
public sealed class PetBrain(
    IIdleDetector idleDetector,
    ISystemLoadMonitor loadMonitor,
    LoadReactionPolicy loadPolicy,
    IFullscreenDetector fullscreenDetector,
    SedentaryReminderPolicy sedentary,
    PetMessageQueue messages,
    PetBehaviorStateMachine stateMachine,
    PetVisibility visibility,
    ChatterPolicy chatter,
    DevScenarios devScenarios,
    SystemScenarios systemScenarios)
{
    /// <summary>
    /// How long the user may be away from keyboard and mouse and still count as present for casual remarks.
    /// </summary>
    private static readonly TimeSpan ChatterPresenceLimit = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long a casual remark stays up; much shorter than a reminder, since there is nothing to act on.
    /// </summary>
    private static readonly TimeSpan ChatterLifetime = TimeSpan.FromSeconds(8);

    /// <summary>
    /// How long a remark that goes with something the pet is acting out stays up.
    /// </summary>
    private static readonly TimeSpan RemarkLifetime = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Whether, as of the last <see cref="Tick"/>, a remark may be shown: the pet is on screen and the user must not
    /// be left undisturbed by nothing.
    /// </summary>
    private bool _canSpeak;

    /// <summary>
    /// Raised when the message on screen appears, changes or goes away. Read <see cref="CurrentMessage"/>.
    /// </summary>
    public event EventHandler? MessageChanged;

    /// <summary>
    /// Raised when <see cref="PetVisibility.IsVisible"/> changes. The window shows or hides itself in response.
    /// </summary>
    public event EventHandler? VisibilityChanged;

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public PetState State => stateMachine.State;

    /// <summary>
    /// Gets the direction of the current or most recent walk: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    public int WalkDirection => stateMachine.WalkDirection;

    /// <summary>
    /// Gets whether the pet is on screen and why not. Change it through <see cref="SetManuallyHidden"/> and
    /// <see cref="ToggleManuallyHidden"/>.
    /// </summary>
    public PetVisibility Visibility => visibility;

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
        var load = loadMonitor.GetSnapshot();
        var isStrained = loadPolicy.Update(load);

        if (sedentary.Update(userIdle))
        {
            messages.Enqueue(new PetMessage(
                PetMessageKind.SedentaryReminder,
                [MessageAction.SnoozeReminder, MessageAction.Dismiss],
                sedentary.StreakDuration));
        }

        // The pet hides for a full-screen application; its messages are held back for that and for quieter
        // situations such as Focus Assist, and for as long as the pet is hidden, then shown once it is back.
        var level = fullscreenDetector.GetLevel();
        if (visibility.UpdateFullscreen(level == DoNotDisturbLevel.Fullscreen))
        {
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }

        _canSpeak = visibility.IsVisible && level == DoNotDisturbLevel.None;

        // What the user is doing and what is going on around them: some of it is acted out once, some for as long
        // as it lasts. A one-off from the system (a welcome) wins over one from development work.
        var dev = devScenarios.Evaluate(userIdle, load);
        var system = systemScenarios.Evaluate(
            userIdle,
            canDisturb: _canSpeak && stateMachine.State is not (PetState.Drag or PetState.Alert or PetState.Sleep) && !stateMachine.State.IsFalling());
        if (dev.Moment is { } devMoment)
        {
            stateMachine.OnMoment(devMoment.State, devMoment.Duration);
        }

        if (system.Moment is { } systemMoment)
        {
            stateMachine.OnMoment(systemMoment.State, systemMoment.Duration);
        }

        if (system.Remark is { } systemRemark)
        {
            Say(systemRemark, system.RemarkVariant);
        }
        else if (dev.Remark is { } devRemark)
        {
            Say(devRemark);
        }

        var ambient = ScenarioCatalog.AmbientRanking.Pick([.. new[] { dev.Ambient, system.Ambient }.OfType<PetState>()]);

        // A casual remark is only worth making when nothing else is on screen or waiting and the user is at the desk
        // and not in a meeting.
        var canChat = visibility.IsVisible
            && level == DoNotDisturbLevel.None
            && ambient != PetState.Meeting
            && userIdle < ChatterPresenceLimit
            && messages.Current is null
            && messages.WaitingCount == 0;
        if (chatter.Update(canChat) is { } remark)
        {
            messages.Enqueue(new PetMessage(PetMessageKind.Chatter, [], Count: remark, Lifetime: ChatterLifetime));
        }

        if (messages.Update(level != DoNotDisturbLevel.None || !visibility.IsVisible))
        {
            MessageChanged?.Invoke(this, EventArgs.Empty);
        }

        var inputs = new PetInputs(
            userIdle,
            isStrained,
            HasAlert: messages.Current is { Kind: not (PetMessageKind.Chatter or PetMessageKind.Remark) },
            IsDeveloping: dev.IsDeveloping,
            Ambient: ambient);
        return stateMachine.Update(inputs);
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
    /// Makes the pet act out a state for a while, such as a finished build or a user who is back. Unlike a state that
    /// follows from the sensors, this one is chosen by the caller and lasts a fixed time. Ignored while the pet is
    /// being dragged.
    /// </summary>
    /// <param name="state">
    /// The state to act out.
    /// </param>
    /// <param name="duration">
    /// How long to stay in it.
    /// </param>
    public void ActOut(PetState state, TimeSpan duration)
    {
        stateMachine.OnMoment(state, duration);
    }

    /// <summary>
    /// Shows a short remark that goes with a state the pet is acting out, unless the pet is hidden or the user must
    /// not be disturbed. Unlike a reminder it is not queued for later: a remark that is no longer about what just
    /// happened would be worse than none. Call from within a <see cref="Tick"/> or after one.
    /// </summary>
    /// <param name="state">
    /// The state the remark belongs to; the UI finds the wording from it.
    /// </param>
    /// <param name="variant">
    /// Which wording of the state's remark, for states that have several (see <see cref="RemarkCode"/>).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the remark was queued.
    /// </returns>
    public bool Say(PetState state, int variant = 0)
    {
        if (!_canSpeak)
        {
            return false;
        }

        messages.Enqueue(new PetMessage(PetMessageKind.Remark, [], Count: RemarkCode.Encode(state, variant), Lifetime: RemarkLifetime));
        return true;
    }

    /// <summary>
    /// Notifies the brain that the pet reached the mouse pointer it was chasing, which makes it happy.
    /// </summary>
    public void OnReachedPointer()
    {
        stateMachine.OnDelighted();
    }

    /// <summary>
    /// Hides or shows the pet at the user's request.
    /// </summary>
    /// <param name="hidden">
    /// <see langword="true"/> to hide the pet.
    /// </param>
    public void SetManuallyHidden(bool hidden)
    {
        if (visibility.SetManuallyHidden(hidden))
        {
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Switches the user's hide on or off.
    /// </summary>
    public void ToggleManuallyHidden()
    {
        SetManuallyHidden(!visibility.IsManuallyHidden);
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
    /// Notifies the brain that the pet was dropped from a height and started to fall.
    /// </summary>
    public void OnFallStarted()
    {
        stateMachine.OnFallStarted();
    }

    /// <summary>
    /// Notifies the brain that the falling pet has opened its umbrella or parachute.
    /// </summary>
    public void OnFallOpened()
    {
        stateMachine.OnFallOpened();
    }

    /// <summary>
    /// Notifies the brain that the falling pet has landed.
    /// </summary>
    public void OnFallEnded()
    {
        stateMachine.OnFallEnded();
    }

    /// <summary>
    /// Puts the pet back to standing, whatever it was doing, as when it is replaced by a new one.
    /// </summary>
    public void ResetState()
    {
        stateMachine.Reset();
    }

    /// <summary>
    /// Notifies the brain that the pet started to crawl along an edge of the screen.
    /// </summary>
    /// <param name="state">
    /// <see cref="PetState.Climb"/> or <see cref="PetState.ClimbDown"/> on a side of the screen, <see cref="PetState.Ceiling"/> on
    /// the top edge.
    /// </param>
    public void OnCrawlStarted(PetState state)
    {
        stateMachine.OnCrawlStarted(state);
    }

    /// <summary>
    /// Notifies the brain that the pet stopped crawling along the edges of the screen.
    /// </summary>
    public void OnCrawlEnded()
    {
        stateMachine.OnCrawlEnded();
    }

    /// <summary>
    /// Notifies the brain that the user clicked the pet. Counts as acknowledging the message on screen.
    /// </summary>
    public void OnClicked()
    {
        DismissMessage();
        stateMachine.OnClicked();
    }
}
