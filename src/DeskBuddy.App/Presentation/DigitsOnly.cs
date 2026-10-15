using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Makes a text box take digits only: other characters typed, pasted or dropped into it are refused, and input methods
/// that compose text (such as Chinese ones) are turned off for it.
/// </summary>
public static class DigitsOnly
{
    /// <summary>
    /// Switches the digits-only behavior on a text box on or off.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(DigitsOnly),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// Gets whether a text box takes digits only.
    /// </summary>
    /// <param name="element">
    /// The text box.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if it takes digits only.
    /// </returns>
    public static bool GetIsEnabled(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Sets whether a text box takes digits only.
    /// </summary>
    /// <param name="element">
    /// The text box.
    /// </param>
    /// <param name="value">
    /// Whether it takes digits only.
    /// </param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Tells whether a text consists of digits only.
    /// </summary>
    /// <param name="text">
    /// The text.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if every character is an ASCII digit; an empty text counts as such.
    /// </returns>
    public static bool IsDigits(string? text)
    {
        return text is null || text.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Hooks or unhooks the events of a text box.
    /// </summary>
    /// <param name="d">
    /// The text box.
    /// </param>
    /// <param name="e">
    /// What changed.
    /// </param>
    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            box.PreviewTextInput += OnPreviewTextInput;
            box.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(box, OnPasting);
            InputMethod.SetIsInputMethodEnabled(box, false);
        }
        else
        {
            box.PreviewTextInput -= OnPreviewTextInput;
            box.PreviewKeyDown -= OnPreviewKeyDown;
            DataObject.RemovePastingHandler(box, OnPasting);
            InputMethod.SetIsInputMethodEnabled(box, true);
        }
    }

    /// <summary>
    /// Refuses typed text that is not digits.
    /// </summary>
    /// <param name="sender">
    /// The text box.
    /// </param>
    /// <param name="e">
    /// The typed text.
    /// </param>
    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsDigits(e.Text);
    }

    /// <summary>
    /// Refuses the space key, which does not raise a text input event of its own.
    /// </summary>
    /// <param name="sender">
    /// The text box.
    /// </param>
    /// <param name="e">
    /// The key.
    /// </param>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Refuses pasted or dropped text that is not digits.
    /// </summary>
    /// <param name="sender">
    /// The text box.
    /// </param>
    /// <param name="e">
    /// The pasted data.
    /// </param>
    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!AcceptsData(e.DataObject))
        {
            e.CancelCommand();
        }
    }

    /// <summary>
    /// Tells whether pasted or dropped data may go into the text box: it must be text, and digits only.
    /// </summary>
    /// <param name="data">
    /// The data.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if it is digits only.
    /// </returns>
    internal static bool AcceptsData(IDataObject data)
    {
        return data.GetDataPresent(DataFormats.UnicodeText) && IsDigits(data.GetData(DataFormats.UnicodeText) as string);
    }
}
