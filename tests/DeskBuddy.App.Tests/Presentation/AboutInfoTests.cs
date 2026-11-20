using System.Globalization;
using DeskBuddy.App.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests.Presentation;

/// <summary>
/// Tests for the about view and the version text it shows.
/// </summary>
[Collection("InterfaceLanguage")]
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
            var view = new AboutView();

            // Assert
            var logo = (Image)view.FindName("LogoImage");
            ((BitmapSource)logo.Source).PixelWidth.Should().BeGreaterThanOrEqualTo(128);
        });
    }

    /// <summary>
    /// The view lays out on whole pixels, so its centered text is not blurred by half-pixel positions.
    /// </summary>
    [Fact]
    public void Constructor_Layout_SnapsToWholePixels()
    {
        StaThread.Run(() =>
        {
            // Act
            var view = new AboutView();

            // Assert
            view.UseLayoutRounding.Should().BeTrue();
            view.SnapsToDevicePixels.Should().BeTrue();
        });
    }

    /// <summary>
    /// The link in the view points to the project page.
    /// </summary>
    [Fact]
    public void Constructor_ProjectLink_PointsToTheProjectPage()
    {
        StaThread.Run(() =>
        {
            // Act
            var view = new AboutView();

            // Assert
            var link = (System.Windows.Documents.Hyperlink)view.FindName("ProjectLink");
            link.NavigateUri.Should().Be(AboutInfo.ProjectUri);
            ((System.Windows.Documents.Run)view.FindName("ProjectLinkText")).Text.Should().Be(AboutInfo.ProjectUri.AbsoluteUri);
        });
    }

    /// <summary>
    /// The view builds from its markup and shows the running version.
    /// </summary>
    [Fact]
    public void Constructor_ShowsVersionOfTheApplication()
    {
        StaThread.Run(() =>
        {
            // Act
            var view = new AboutView();

            // Assert
            var version = (TextBlock)view.FindName("VersionText");
            version.Text.Should().Contain(AboutInfo.CurrentVersion());
        });
    }

    /// <summary>
    /// The texts of the markup follow a change of the interface language without the view being made anew.
    /// </summary>
    [Fact]
    public void LanguageChanged_Notified_TextsOfTheMarkupAreWrittenAgain()
    {
        StaThread.Run(() =>
        {
            // Arrange
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var view = new AboutView();
            var button = (Button)view.FindName("CheckUpdatesButton");
            var before = button.Content;

            // Act
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            LocalizedStrings.Instance.NotifyLanguageChanged();

            // Assert
            button.Content.Should().Be(Strings.AboutCheckUpdates);
            button.Content.Should().NotBe(before);
        });
    }

    /// <summary>
    /// Without an update controller, the view offers no check for updates.
    /// </summary>

    [Fact]
    public void Attach_WithoutUpdates_HidesTheUpdateCard()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var view = new AboutView();

            // Act
            view.Attach(null);

            // Assert
            var card = (StackPanel)view.FindName("UpdateCard");
            card.Visibility.Should().Be(Visibility.Collapsed);
        });
    }
}
