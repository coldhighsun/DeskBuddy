namespace DeskBuddy.Domain.Messages;

/// <summary>
/// What a <see cref="PetMessage"/> is about. The UI maps each kind to localized text.
/// </summary>
public enum PetMessageKind
{
    /// <summary>
    /// The user has been working for a long time without a break.
    /// </summary>
    SedentaryReminder,

    /// <summary>
    /// A casual remark from the pet, with nothing for the user to do. <see cref="PetMessage.Count"/> selects which
    /// remark.
    /// </summary>
    Chatter,

    /// <summary>
    /// A short remark that goes with something the pet is acting out. <see cref="PetMessage.Count"/> holds the
    /// <see cref="RemarkCode"/> of the <see cref="PetState"/> the remark belongs to and its wording.
    /// </summary>
    Remark,
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
/// A duration the text refers to, for example how long the user has been working.
/// </param>
/// <param name="Count">
/// A count the text refers to, for example which remark to show.
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
