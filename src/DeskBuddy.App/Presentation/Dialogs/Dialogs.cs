using System.Windows;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Shows DeskBuddy's own dialogs, which replace the classic message boxes so that they match the themed windows. Must be
/// used on the UI thread.
/// </summary>
internal static class Dialogs
{
    /// <summary>
    /// Shows a dialog and waits for the answer.
    /// </summary>
    /// <param name="owner">The window the dialog belongs to; <see langword="null"/> centers it on the screen.</param>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The text of the dialog.</param>
    /// <param name="kind">The kind of dialog, which decides its icon.</param>
    /// <param name="buttons">The buttons to offer.</param>
    /// <returns>The answer the user gave.</returns>
    public static DialogChoice Show(
        Window? owner,
        string title,
        string message,
        DialogKind kind,
        IReadOnlyList<DialogButton> buttons)
    {
        var window = new DialogWindow(title, message, kind, buttons);
        if (owner is { IsVisible: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        _ = window.ShowDialog();
        return window.Choice;
    }
}
