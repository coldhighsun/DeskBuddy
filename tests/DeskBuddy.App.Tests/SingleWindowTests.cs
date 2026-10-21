using System.Windows;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="SingleWindow{TWindow}"/>.
/// </summary>
public class SingleWindowTests
{
    /// <summary>
    /// Nothing is created until the window is first asked for.
    /// </summary>
    [Fact]
    public void Constructor_NoOpen_CreatesNothing()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var created = 0;

            // Act
            _ = new SingleWindow<Window>(() => { created++; return new Window(); }, _ => { });

            // Assert
            created.Should().Be(0);
        });
    }

    /// <summary>
    /// Opening twice while the window is open shows the same window, and creates it only once.
    /// </summary>
    [Fact]
    public void Open_WindowStillOpen_ShowsTheSameWindow()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var created = 0;
            var shown = new List<Window>();
            var single = new SingleWindow<Window>(() => { created++; return new Window(); }, shown.Add);

            // Act
            single.Open();
            single.Open();

            // Assert
            created.Should().Be(1);
            shown.Should().HaveCount(2);
            shown[1].Should().BeSameAs(shown[0]);
        });
    }

    /// <summary>
    /// After the window was closed, the next open creates a new window instead of showing the closed one.
    /// </summary>
    [Fact]
    public void Open_AfterWindowClosed_CreatesANewWindow()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var shown = new List<Window>();
            var single = new SingleWindow<Window>(() => new Window(), shown.Add);
            single.Open();
            shown[0].Close();

            // Act
            single.Open();

            // Assert
            shown.Should().HaveCount(2);
            shown[1].Should().NotBeSameAs(shown[0]);
        });
    }
}
