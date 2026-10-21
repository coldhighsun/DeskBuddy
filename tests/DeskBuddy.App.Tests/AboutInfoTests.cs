using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for the about window and the version text it shows.
/// </summary>
public class AboutInfoTests
{
    /// <summary>
    /// The build metadata after the plus sign is not part of the version shown.
    /// </summary>
    [Theory]
    [InlineData("1.2.3+abc1234", "1.2.3")]
    [InlineData("1.2.3-alpha.0.5+abc1234", "1.2.3-alpha.0.5")]
    [InlineData("1.2.3", "1.2.3")]
    public void FormatVersion_WithOrWithoutMetadata_ReturnsVersionOnly(string input, string expected)
    {
        // Act
        var result = AboutInfo.FormatVersion(input);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// A missing version shows as a question mark instead of an empty line.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("+abc")]
    public void FormatVersion_NothingUsable_ReturnsQuestionMark(string? input)
    {
        // Act
        var result = AboutInfo.FormatVersion(input);

        // Assert
        result.Should().Be("?");
    }

    /// <summary>
    /// The logo is the largest frame of the icon, not the 16 pixel one an <c>Image</c> would pick by itself.
    /// </summary>
    [Fact]
    public void Constructor_Logo_IsTheLargestIconFrame()
    {
        StaThread.Run(() =>
        {
            // Act
            var window = new AboutWindow();

            // Assert
            var logo = (Image)window.FindName("LogoImage");
            ((BitmapSource)logo.Source).PixelWidth.Should().BeGreaterThanOrEqualTo(128);
            window.Close();
        });
    }

    /// <summary>
    /// Pressing the close button closes the window.
    /// </summary>
    [Fact]
    public void CloseButton_Click_ClosesTheWindow()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var window = new AboutWindow();
            var closed = false;
            window.Closed += (_, _) => closed = true;
            window.ShowAbout();
            var button = (Button)window.FindName("CloseButton");

            try
            {
                // Act
                button.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));

                // Assert
                closed.Should().BeTrue();
            }
            finally
            {
                // A window that is still open when the assertion fails must not outlive the test.
                window.Close();
            }
        });
    }

    /// <summary>
    /// The window lays out on whole pixels, so its centered text is not blurred by half-pixel positions.
    /// </summary>
    [Fact]
    public void Constructor_Layout_SnapsToWholePixels()
    {
        StaThread.Run(() =>
        {
            // Act
            var window = new AboutWindow();

            // Assert
            window.UseLayoutRounding.Should().BeTrue();
            window.SnapsToDevicePixels.Should().BeTrue();
            window.Close();
        });
    }

    /// <summary>
    /// The link in the window points to the project page.
    /// </summary>
    [Fact]
    public void Constructor_ProjectLink_PointsToTheProjectPage()
    {
        StaThread.Run(() =>
        {
            // Act
            var window = new AboutWindow();

            // Assert
            var link = (System.Windows.Documents.Hyperlink)window.FindName("ProjectLink");
            link.NavigateUri.Should().Be(AboutInfo.ProjectUri);
            window.Close();
        });
    }

    /// <summary>
    /// The window builds from its markup and shows the running version.
    /// </summary>
    [Fact]
    public void Constructor_ShowsVersionOfTheApplication()
    {
        StaThread.Run(() =>
        {
            // Act
            var window = new AboutWindow();

            // Assert
            var version = (TextBlock)window.FindName("VersionText");
            version.Text.Should().Contain(AboutInfo.CurrentVersion());
            window.Close();
        });
    }
}
