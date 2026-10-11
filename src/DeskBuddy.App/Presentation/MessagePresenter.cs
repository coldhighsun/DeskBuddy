using System.Globalization;
using DeskBuddy.App.Resources;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Messages;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// A button to show in a speech bubble.
/// </summary>
/// <param name="Action">
/// What the button does when clicked.
/// </param>
/// <param name="Label">
/// The button text.
/// </param>
public readonly record struct BubbleButton(MessageAction Action, string Label);

/// <summary>
/// What a speech bubble shows for a message.
/// </summary>
/// <param name="Text">
/// The localized message text.
/// </param>
/// <param name="Buttons">
/// The buttons, in display order.
/// </param>
public sealed record BubbleContent(string Text, IReadOnlyList<BubbleButton> Buttons);

/// <summary>
/// Turns language-neutral <see cref="PetMessage"/> values into localized bubble content.
/// </summary>
public static class MessagePresenter
{
    /// <summary>
    /// Builds the bubble content for a message.
    /// </summary>
    /// <param name="message">
    /// The message to present.
    /// </param>
    /// <returns>
    /// The localized text and buttons.
    /// </returns>
    public static BubbleContent Present(PetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var text = message.Kind switch
        {
            PetMessageKind.PomodoroWorkCompleted => string.Format(
                CultureInfo.CurrentCulture,
                Strings.MessageWorkCompleted,
                WholeMinutes(message.Duration ?? TimeSpan.Zero),
                message.Count ?? 0),
            PetMessageKind.PomodoroBreakCompleted => Strings.MessageBreakCompleted,
            PetMessageKind.SedentaryReminder => string.Format(
                CultureInfo.CurrentCulture,
                Strings.MessageSedentary,
                WholeMinutes(message.Duration ?? TimeSpan.Zero)),
            PetMessageKind.Chatter => Strings.Chatter(message.Count ?? 0),
            PetMessageKind.Remark => Strings.Remark(message.Count ?? 0),
            _ => string.Empty,
        };

        var buttons = message.Actions.Select(action => new BubbleButton(action, LabelFor(action))).ToList();
        return new BubbleContent(text, buttons);
    }

    /// <summary>
    /// Rounds a duration up to whole minutes, so "4 min 59 s" reads as 5.
    /// </summary>
    /// <param name="duration">
    /// The duration to round.
    /// </param>
    /// <returns>
    /// The number of minutes, rounded up.
    /// </returns>
    public static int WholeMinutes(TimeSpan duration)
    {
        return (int)Math.Ceiling(duration.TotalMinutes);
    }

    /// <summary>
    /// Gets the button label for an action.
    /// </summary>
    /// <param name="action">
    /// The action.
    /// </param>
    /// <returns>
    /// The localized label.
    /// </returns>
    private static string LabelFor(MessageAction action)
    {
        return action switch
        {
            MessageAction.Dismiss => Strings.ActionDismiss,
            MessageAction.SkipBreak => Strings.ActionSkipBreak,
            MessageAction.StartPomodoro => Strings.ActionStartPomodoro,
            MessageAction.SnoozeReminder => Strings.ActionSnoozeReminder,
            _ => string.Empty,
        };
    }
}
