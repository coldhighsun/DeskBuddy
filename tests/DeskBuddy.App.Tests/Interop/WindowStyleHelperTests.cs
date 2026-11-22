using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests.Interop;

/// <summary>
/// Tests for <see cref="WindowStyleHelper"/>.
/// </summary>
public sealed class WindowStyleHelperTests
{
    /// <summary>
    /// A change of monitors, resolution, scaling or work area is a change of the screen layout.
    /// </summary>
    /// <param name="msg">The window message.</param>
    /// <param name="wParam">Its <c>wParam</c>.</param>
    [Theory]
    [InlineData(WindowStyleHelper.WmDisplayChange, 0)]
    [InlineData(WindowStyleHelper.WmDpiChanged, 0)]
    [InlineData(WindowStyleHelper.WmSettingChange, WindowStyleHelper.SpiSetWorkArea)]
    public void IsScreenLayoutChange_LayoutMessages_ReturnsTrue(int msg, int wParam)
    {
        // Act
        var result = WindowStyleHelper.IsScreenLayoutChange(msg, wParam);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Other messages, and other system settings changing, are not.
    /// </summary>
    /// <param name="msg">The window message.</param>
    /// <param name="wParam">Its <c>wParam</c>.</param>
    [Theory]
    [InlineData(WindowStyleHelper.WmSettingChange, 0)]
    [InlineData(WindowStyleHelper.WmSettingChange, 0x0057)]
    [InlineData(WindowStyleHelper.WmNcHitTest, 0)]
    public void IsScreenLayoutChange_OtherMessages_ReturnsFalse(int msg, int wParam)
    {
        // Act
        var result = WindowStyleHelper.IsScreenLayoutChange(msg, wParam);

        // Assert
        result.Should().BeFalse();
    }
}
