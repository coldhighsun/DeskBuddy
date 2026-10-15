using System.Windows.Input;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="HotkeyCapture"/>.
/// </summary>
public class HotkeyCaptureTests
{
    /// <summary>
    /// A letter with Ctrl and Alt is accepted as the matching shortcut.
    /// </summary>
    [Fact]
    public void TryCreate_LetterWithModifiers_IsAccepted()
    {
        // Act
        var result = HotkeyCapture.TryCreate(Key.H, Key.None, ModifierKeys.Control | ModifierKeys.Alt, out var gesture);

        // Assert
        result.Should().Be(HotkeyCaptureResult.Accepted);
        gesture.Should().Be(new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'H'));
    }

    /// <summary>
    /// With Alt held WPF reports the real key as the system key, which is the one that counts.
    /// </summary>
    [Fact]
    public void TryCreate_AltCombination_UsesSystemKey()
    {
        // Act
        var result = HotkeyCapture.TryCreate(Key.System, Key.F12, ModifierKeys.Alt, out var gesture);

        // Assert
        result.Should().Be(HotkeyCaptureResult.Accepted);
        gesture.ToString().Should().Be("Alt+F12");
    }

    /// <summary>
    /// The Windows and Shift modifiers are carried over.
    /// </summary>
    [Fact]
    public void TryCreate_WindowsAndShift_AreMapped()
    {
        // Act
        var result = HotkeyCapture.TryCreate(Key.D5, Key.None, ModifierKeys.Windows | ModifierKeys.Shift, out var gesture);

        // Assert
        result.Should().Be(HotkeyCaptureResult.Accepted);
        gesture.ToString().Should().Be("Shift+Win+5");
    }

    /// <summary>
    /// Pressing only a modifier key keeps waiting for the real key.
    /// </summary>
    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.LWin)]
    public void TryCreate_ModifierOnly_KeepsWaiting(Key key)
    {
        // Act
        var result = HotkeyCapture.TryCreate(key, Key.None, ModifierKeys.Control, out _);

        // Assert
        result.Should().Be(HotkeyCaptureResult.Waiting);
    }

    /// <summary>
    /// With Alt held, pressing only Alt itself also keeps waiting.
    /// </summary>
    [Fact]
    public void TryCreate_AltAlone_KeepsWaiting()
    {
        // Act
        var result = HotkeyCapture.TryCreate(Key.System, Key.LeftAlt, ModifierKeys.Alt, out _);

        // Assert
        result.Should().Be(HotkeyCaptureResult.Waiting);
    }

    /// <summary>
    /// A plain key without any modifier is refused, so that typing is never taken over system-wide.
    /// </summary>
    [Fact]
    public void TryCreate_NoModifier_IsRejected()
    {
        // Act
        var result = HotkeyCapture.TryCreate(Key.H, Key.None, ModifierKeys.None, out _);

        // Assert
        result.Should().Be(HotkeyCaptureResult.Rejected);
    }

    /// <summary>
    /// A key that shortcuts do not support, such as Enter, is refused.
    /// </summary>
    [Fact]
    public void TryCreate_UnsupportedKey_IsRejected()
    {
        // Act
        var result = HotkeyCapture.TryCreate(Key.Enter, Key.None, ModifierKeys.Control, out _);

        // Assert
        result.Should().Be(HotkeyCaptureResult.Rejected);
    }
}
