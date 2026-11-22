using DeskBuddy.App.Resources;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// What the user chose in a dialog.
/// </summary>
internal enum DialogChoice
{
    /// <summary>
    /// The dialog was confirmed; it only has this one answer.
    /// </summary>
    Ok,

    /// <summary>
    /// The user agreed, or chose the first of two answers.
    /// </summary>
    Yes,

    /// <summary>
    /// The user declined, or chose the second of two answers.
    /// </summary>
    No,

    /// <summary>
    /// The user backed out and wants nothing to happen.
    /// </summary>
    Cancel,
}

/// <summary>
/// The kind of a dialog, which decides its icon.
/// </summary>
internal enum DialogKind
{
    /// <summary>
    /// A plain notice.
    /// </summary>
    Information,

    /// <summary>
    /// A question the user answers.
    /// </summary>
    Question,

    /// <summary>
    /// A warning about something that may lose work.
    /// </summary>
    Warning,

    /// <summary>
    /// An error message.
    /// </summary>
    Error,
}

/// <summary>
/// One button of a dialog.
/// </summary>
/// <param name="Label">The text on the button.</param>
/// <param name="Choice">The answer the button gives.</param>
/// <param name="IsPrimary">Whether it is the suggested answer, drawn in the accent color and chosen by Enter.</param>
internal sealed record DialogButton(string Label, DialogChoice Choice, bool IsPrimary);

/// <summary>
/// The parts of a dialog that can be worked out without showing anything: its buttons, its icon, and what closing it
/// without a button means.
/// </summary>
internal static class DialogModel
{
    /// <summary>
    /// Builds the buttons of a dialog with a single answer.
    /// </summary>
    /// <returns>One primary button.</returns>
    public static IReadOnlyList<DialogButton> OkButtons()
    {
        return [new DialogButton(Strings.DialogOk, DialogChoice.Ok, true)];
    }

    /// <summary>
    /// Builds the buttons of a yes-or-no question.
    /// </summary>
    /// <returns>A primary Yes button and a No button.</returns>
    public static IReadOnlyList<DialogButton> YesNoButtons()
    {
        return
        [
            new DialogButton(Strings.DialogYes, DialogChoice.Yes, true),
            new DialogButton(Strings.DialogNo, DialogChoice.No, false),
        ];
    }

    /// <summary>
    /// Builds the buttons of the question asked when unsaved changes are about to be lost.
    /// </summary>
    /// <returns>A primary Save button, a button that drops the changes and a Cancel button.</returns>
    public static IReadOnlyList<DialogButton> SaveDiscardCancelButtons()
    {
        return
        [
            new DialogButton(Strings.SettingsSave, DialogChoice.Yes, true),
            new DialogButton(Strings.DialogDontSave, DialogChoice.No, false),
            new DialogButton(Strings.DialogCancel, DialogChoice.Cancel, false),
        ];
    }

    /// <summary>
    /// Works out what closing the dialog without pressing a button (Esc, the close box) means.
    /// </summary>
    /// <param name="buttons">The buttons of the dialog.</param>
    /// <returns>Cancel when there is a Cancel button, otherwise No when there is a No button, otherwise Ok.</returns>
    public static DialogChoice CloseChoice(IReadOnlyList<DialogButton> buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        if (buttons.Any(button => button.Choice == DialogChoice.Cancel))
        {
            return DialogChoice.Cancel;
        }

        return buttons.Any(button => button.Choice == DialogChoice.No) ? DialogChoice.No : DialogChoice.Ok;
    }

    /// <summary>
    /// Gets the glyph, in the Segoe Fluent Icons font, that stands for a kind of dialog.
    /// </summary>
    /// <param name="kind">The kind of dialog.</param>
    /// <returns>The glyph character.</returns>
    public static string Glyph(DialogKind kind)
    {
        return kind switch
        {
            DialogKind.Question => "",
            DialogKind.Warning => "",
            DialogKind.Error => "",
            _ => "",
        };
    }
}
