using AwesomeAssertions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="DigitsOnly"/>.
/// </summary>
public class DigitsOnlyTests
{
    /// <summary>
    /// Digits, and nothing at all, are accepted.
    /// </summary>
    /// <param name="text">
    /// The text.
    /// </param>
    [Theory]
    [InlineData("45")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData(null)]
    public void IsDigits_DigitsOrNothing_IsTrue(string? text)
    {
        DigitsOnly.IsDigits(text).Should().BeTrue();
    }

    /// <summary>
    /// Letters, spaces, signs, separators and non-ASCII digits are refused.
    /// </summary>
    /// <param name="text">
    /// The text.
    /// </param>
    [Theory]
    [InlineData("4a")]
    [InlineData("4 5")]
    [InlineData("-5")]
    [InlineData("4.5")]
    [InlineData("\u0664\u0665")]
    [InlineData("\uFF14\uFF15")]
    public void IsDigits_OtherCharacters_IsFalse(string text)
    {
        DigitsOnly.IsDigits(text).Should().BeFalse();
    }

    /// <summary>
    /// A typed digit goes through and a typed letter is refused.
    /// </summary>
    /// <param name="typed">
    /// The typed text.
    /// </param>
    /// <param name="refused">
    /// Whether it should be refused.
    /// </param>
    [Theory]
    [InlineData("5", false)]
    [InlineData("a", true)]
    [InlineData("-", true)]
    public void PreviewTextInput_TypedText_IsRefusedUnlessDigits(string typed, bool refused)
    {
        StaThread.Run(() =>
        {
            // Arrange
            var box = new TextBox();
            DigitsOnly.SetIsEnabled(box, true);
            var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, typed))
            {
                RoutedEvent = UIElement.PreviewTextInputEvent,
            };

            // Act
            box.RaiseEvent(args);

            // Assert
            args.Handled.Should().Be(refused);
        });
    }

    /// <summary>
    /// The space key is refused, as it raises no text input event of its own.
    /// </summary>
    [Fact]
    public void PreviewKeyDown_Space_IsRefused()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var box = new TextBox();
            DigitsOnly.SetIsEnabled(box, true);
            using var source = new HwndSource(new HwndSourceParameters());
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };

            // Act
            box.RaiseEvent(args);

            // Assert
            args.Handled.Should().BeTrue();
        });
    }

    /// <summary>
    /// Switching the behavior off lets other text through again.
    /// </summary>
    [Fact]
    public void PreviewTextInput_BehaviorSwitchedOff_IsNotRefused()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var box = new TextBox();
            DigitsOnly.SetIsEnabled(box, true);
            DigitsOnly.SetIsEnabled(box, false);
            var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, "a"))
            {
                RoutedEvent = UIElement.PreviewTextInputEvent,
            };

            // Act
            box.RaiseEvent(args);

            // Assert
            args.Handled.Should().BeFalse();
        });
    }

    /// <summary>
    /// Pasted data is accepted only when it is text made of digits.
    /// </summary>
    /// <param name="text">
    /// The pasted text.
    /// </param>
    /// <param name="accepted">
    /// Whether it should be accepted.
    /// </param>
    [Theory]
    [InlineData("123", true)]
    [InlineData("12a", false)]
    [InlineData("1 2", false)]
    public void AcceptsData_PastedText_IsAcceptedOnlyWhenDigits(string text, bool accepted)
    {
        // Act
        var result = DigitsOnly.AcceptsData(new DataObject(DataFormats.UnicodeText, text));

        // Assert
        result.Should().Be(accepted);
    }

    /// <summary>
    /// Pasted data that is not text is refused.
    /// </summary>
    [Fact]
    public void AcceptsData_NotText_IsRefused()
    {
        // Act
        var result = DigitsOnly.AcceptsData(new DataObject(DataFormats.Bitmap, new object()));

        // Assert
        result.Should().BeFalse();
    }
}
