namespace DeskBuddy.Domain.Messages;

/// <summary>
/// What a <see cref="PetMessage"/> is about. The UI maps each kind to localized text.
/// </summary>
public enum PetMessageKind
{
    /// <summary>
    /// A pomodoro work phase finished and a break has started.
    /// </summary>
    PomodoroWorkCompleted,

    /// <summary>
    /// A pomodoro break finished; the user can start the next round.
    /// </summary>
    PomodoroBreakCompleted,

    /// <summary>
    /// The user has been working for a long time without a break.
    /// </summary>
    SedentaryReminder,

    /// <summary>
    /// A casual remark from the pet, with nothing for the user to do. <see cref="PetMessage.Count"/> selects which
    /// remark.
    /// </summary>
    Chatter,
}

/// <summary>
/// Something the user can do from a message bubble.
/// </summary>
public enum MessageAction
{
    /// <summary>
    /// Acknowledge and close the message.
    /// </summary>
    Dismiss,

    /// <summary>
    /// Cut the current break short.
    /// </summary>
    SkipBreak,

    /// <summary>
    /// Start a new pomodoro round.
    /// </summary>
    StartPomodoro,

    /// <summary>
    /// Ask to be reminded again soon.
    /// </summary>
    SnoozeReminder,
}

/// <summary>
/// A short notice the pet shows in a speech bubble. Carries data, not text: wording lives in the UI layer.
/// </summary>
/// <param name="Kind">
/// What the message is about.
/// </param>
/// <param name="Actions">
/// The actions offered to the user, in display order.
/// </param>
/// <param name="Duration">
/// A duration the text refers to, for example the length of the break that just started.
/// </param>
/// <param name="Count">
/// A count the text refers to, for example the number of completed pomodoros.
/// </param>
/// <param name="Lifetime">
/// How long the message stays up; <see langword="null"/> for the queue's default.
/// </param>
public sealed record PetMessage(
    PetMessageKind Kind,
    IReadOnlyList<MessageAction> Actions,
    TimeSpan? Duration = null,
    int? Count = null,
    TimeSpan? Lifetime = null);
