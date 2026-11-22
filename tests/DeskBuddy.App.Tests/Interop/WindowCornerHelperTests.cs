using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests.Interop;

/// <summary>
/// Tests for <see cref="WindowCornerHelper"/>.
/// </summary>
public sealed class WindowCornerHelperTests
{
    /// <summary>
    /// Windows 11 and later draw the rounded corners, the border and the shadow of a borderless window themselves.
    /// </summary>
    /// <param name="build">The Windows build number.</param>
    [Theory]
    [InlineData(22000)]
    [InlineData(26100)]
    public void IsSupported_Windows11Build_ReturnsTrue(int build)
    {
        // Act
        var supported = WindowCornerHelper.IsSupported(build);

        // Assert
        supported.Should().BeTrue();
    }

    /// <summary>
    /// Windows 10 cannot, so the window draws an outline of its own.
    /// </summary>
    /// <param name="build">The Windows build number.</param>
    [Theory]
    [InlineData(19045)]
    [InlineData(21999)]
    public void IsSupported_Windows10Build_ReturnsFalse(int build)
    {
        // Act
        var supported = WindowCornerHelper.IsSupported(build);

        // Assert
        supported.Should().BeFalse();
    }
}
