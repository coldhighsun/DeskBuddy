using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AwesomeAssertions;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for moving the pet and its canopy together, with real (never shown) windows.
/// </summary>
public sealed class UmbrellaMovementTests
{
    /// <summary>
    /// Creates a window that has a handle but is never shown.
    /// </summary>
    /// <param name="size">
    /// Its width and height in device-independent pixels.
    /// </param>
    /// <returns>
    /// The window.
    /// </returns>
    private static Window CreateWindow(double size)
    {
        var window = new Window { Width = size, Height = size, ShowInTaskbar = false };
        _ = new WindowInteropHelper(window).EnsureHandle();
        return window;
    }

    /// <summary>
    /// Both windows end up where they were told to go after one batched move.
    /// </summary>
    [Fact]
    public void MoveTogetherToPixels_TwoWindows_BothReachTheirPlaces()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var first = CreateWindow(100);
            var second = CreateWindow(80);

            // Act
            WindowMover.MoveTogetherToPixels(first, 120, 210, second, 300, 40);

            // Assert
            var firstBounds = MonitorHelper.GetWindowBoundsPixels(first);
            var secondBounds = MonitorHelper.GetWindowBoundsPixels(second);
            (firstBounds.Left, firstBounds.Top).Should().Be((120, 210));
            (secondBounds.Left, secondBounds.Top).Should().Be((300, 40));
            first.Close();
            second.Close();
        });
    }

    /// <summary>
    /// Moving the pet moves the canopy over it, and moving the pet again moves the canopy by the same distance; a move
    /// to the place it is already at changes nothing.
    /// </summary>
    [Fact]
    public void MoveWithPet_PetMoves_CanopyFollowsToItsPlacement()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var pet = CreateWindow(128);
            var canopy = new UmbrellaWindow();
            var scale = VisualTreeHelper.GetDpi(pet).DpiScaleX;
            var first = new PixelRect(100, 200, 100 + 128, 200 + 128);
            var second = new PixelRect(110, 260, 110 + 128, 260 + 128);

            // Act
            canopy.MoveWithPet(pet, first, scale);
            var afterFirst = MonitorHelper.GetWindowBoundsPixels(canopy);
            canopy.MoveWithPet(pet, second, scale);
            var afterSecond = MonitorHelper.GetWindowBoundsPixels(canopy);
            canopy.MoveWithPet(pet, second, scale);
            var afterRepeat = MonitorHelper.GetWindowBoundsPixels(canopy);

            // Assert
            var expectedFirst = CanopyPlacement.For(first, pet.Width, scale);
            var expectedSecond = CanopyPlacement.For(second, pet.Width, scale);
            (afterFirst.Left, afterFirst.Top).Should().Be((expectedFirst.X, expectedFirst.Y));
            (afterSecond.Left, afterSecond.Top).Should().Be((expectedSecond.X, expectedSecond.Y));
            afterRepeat.Should().Be(afterSecond);
            var petBounds = MonitorHelper.GetWindowBoundsPixels(pet);
            (petBounds.Left, petBounds.Top).Should().Be((110, 260));
            canopy.Close();
            pet.Close();
        });
    }
}
