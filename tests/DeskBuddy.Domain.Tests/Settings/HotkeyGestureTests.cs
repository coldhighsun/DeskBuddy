using AwesomeAssertions;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Domain.Tests.Settings;

/// <summary>
/// Tests for <see cref="HotkeyGesture"/>.
/// </summary>
public class HotkeyGestureTests
{
    /// <summary>
    /// Valid text is parsed into modifiers and a virtual-key code.
    /// </summary>
    [Theory]
    [InlineData("Ctrl+Alt+H", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x48)]
    [InlineData("shift+f12", HotkeyModifiers.Shift, 0x7B)]
    [InlineData(" Control + Win + 5 ", HotkeyModifiers.Control | HotkeyModifiers.Windows, 0x35)]
    [InlineData("Alt+F24", HotkeyModifiers.Alt, 0x87)]
    public void TryParse_ValidText_ReturnsGesture(string text, HotkeyModifiers modifiers, int key)
    {
        // Act
        var parsed = HotkeyGesture.TryParse(text, out var gesture);

        // Assert
        parsed.Should().BeTrue();
        gesture.Should().Be(new HotkeyGesture(modifiers, key));
    }

    /// <summary>
    /// Text that is not a usable shortcut is rejected.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+H+J")]
    [InlineData("Ctrl+Enter")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+Alt+")]
    public void TryParse_InvalidText_ReturnsFalse(string? text)
    {
        // Act
        var parsed = HotkeyGesture.TryParse(text, out _);

        // Assert
        parsed.Should().BeFalse();
    }

    /// <summary>
    /// Formatting uses the canonical modifier order whatever the input order was.
    /// </summary>
    [Fact]
    public void ToString_AfterParse_IsCanonical()
    {
        // Act
        HotkeyGesture.TryParse("win+shift+alt+ctrl+a", out var gesture);

        // Assert
        gesture.ToString().Should().Be("Ctrl+Alt+Shift+Win+A");
    }

    /// <summary>
    /// A gesture without any modifier is not valid.
    /// </summary>
    [Fact]
    public void IsValid_NoModifier_ReturnsFalse()
    {
        // Act
        var gesture = new HotkeyGesture(HotkeyModifiers.None, 'H');

        // Assert
        gesture.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A gesture with an unsupported key is not valid.
    /// </summary>
    [Fact]
    public void IsValid_UnsupportedKey_ReturnsFalse()
    {
        // Act
        var gesture = new HotkeyGesture(HotkeyModifiers.Control, 0x0D);

        // Assert
        gesture.IsValid.Should().BeFalse();
    }
}
