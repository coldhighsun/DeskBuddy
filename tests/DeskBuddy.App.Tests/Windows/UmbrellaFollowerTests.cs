using System.Windows;
using System.Windows.Interop;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests.Windows;

/// <summary>
/// Tests for <see cref="UmbrellaFollower"/> while no canopy is open, with a real (never shown) pet window.
/// </summary>
public sealed class UmbrellaFollowerTests
{
    /// <summary>
    /// Without an open canopy there is no canopy window to report.
    /// </summary>
    [Fact]
    public void OpenCanopy_NothingOpened_IsNull()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var pet = new Window { Width = 64, Height = 64, ShowInTaskbar = false };
            _ = new WindowInteropHelper(pet).EnsureHandle();
            var follower = new UmbrellaFollower(pet);

            // Act
            var canopy = follower.OpenCanopy;

            // Assert
            canopy.Should().BeNull();
            pet.Close();
        });
    }

    /// <summary>
    /// Lifting a canopy that was never opened does nothing and does not make the window.
    /// </summary>
    [Fact]
    public void Raise_NothingOpened_DoesNothing()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var pet = new Window { Width = 64, Height = 64, ShowInTaskbar = false };
            _ = new WindowInteropHelper(pet).EnsureHandle();
            var follower = new UmbrellaFollower(pet);

            // Act
            follower.Raise();

            // Assert
            follower.IsOpen.Should().BeFalse();
            follower.OpenCanopy.Should().BeNull();
            pet.Close();
        });
    }
}
