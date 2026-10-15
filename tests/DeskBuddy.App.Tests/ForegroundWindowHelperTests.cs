using System.Windows;
using System.Windows.Interop;
using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="ForegroundWindowHelper"/>.
/// </summary>
public class ForegroundWindowHelperTests
{
    /// <summary>
    /// A window of this application is recognized as its own.
    /// </summary>
    [Fact]
    public void BelongsToThisProcess_OwnWindow_IsTrue()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var window = new Window();
            var handle = new WindowInteropHelper(window).EnsureHandle();

            // Act
            var result = ForegroundWindowHelper.BelongsToThisProcess(handle);

            // Assert
            result.Should().BeTrue();
            window.Close();
        });
    }

    /// <summary>
    /// No window belongs to no process, so it is not taken for one of this application.
    /// </summary>
    [Fact]
    public void BelongsToThisProcess_NoWindow_IsFalse()
    {
        // Act
        var result = ForegroundWindowHelper.BelongsToThisProcess(0);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// A window that does not exist cannot be brought to the front.
    /// </summary>
    [Fact]
    public void TryActivate_NoWindow_IsFalse()
    {
        // Act
        var result = ForegroundWindowHelper.TryActivate(0);

        // Assert
        result.Should().BeFalse();
    }
}
