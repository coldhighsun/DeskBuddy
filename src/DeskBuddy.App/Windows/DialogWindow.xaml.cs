using System.Windows;
using System.Windows.Controls;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Windows;

/// <summary>
/// A themed replacement for the classic message box: an icon, a message and a row of answer buttons. Use
/// <see cref="Dialogs.Show"/> to show it.
/// </summary>
public partial class DialogWindow : Window
{
    /// <summary>
    /// What closing the window without a button means.
    /// </summary>
    private readonly DialogChoice _closeChoice;

    /// <summary>
    /// Whether the user has already picked an answer.
    /// </summary>
    private bool _answered;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogWindow"/> class.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The text of the dialog.</param>
    /// <param name="kind">The kind of dialog, which decides its icon.</param>
    /// <param name="buttons">The buttons to offer.</param>
    internal DialogWindow(string title, string message, DialogKind kind, IReadOnlyList<DialogButton> buttons)
    {
        InitializeComponent();

        Title = title;
        MessageText.Text = message;
        IconText.Text = DialogModel.Glyph(kind);
        if (kind == DialogKind.Error)
        {
            IconText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorTextBrush");
        }

        _closeChoice = DialogModel.CloseChoice(buttons);
        Choice = _closeChoice;

        for (var i = 0; i < buttons.Count; i++)
        {
            var definition = buttons[i];
            var button = new Button
            {
                Content = definition.Label,
                MinWidth = 80,
                MinHeight = 32,
                Padding = new Thickness(16, 3, 16, 3),
                Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                IsDefault = definition.IsPrimary,
                IsCancel = definition.Choice == _closeChoice && definition.Choice != DialogChoice.Ok,
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(button, $"Dialog_{definition.Choice}");
            button.SetResourceReference(StyleProperty, definition.IsPrimary ? "RaisedButton" : "FlatButton");

            button.Click += (_, _) => Answer(definition.Choice);
            ButtonPanel.Children.Add(button);
        }

        Loaded += (_, _) => Motion.FadeIn((UIElement)Content);
    }

    /// <summary>
    /// Gets the answer the user gave; before an answer it is what closing the window without a button means.
    /// </summary>
    internal DialogChoice Choice { get; private set; }

    /// <summary>
    /// Records an answer and closes the dialog.
    /// </summary>
    /// <param name="choice">The answer given.</param>
    private void Answer(DialogChoice choice)
    {
        _answered = true;
        Choice = choice;
        Close();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (!_answered)
        {
            Choice = _closeChoice;
        }

        base.OnClosed(e);
    }
}
