using System.IO;
using System.Xml.Linq;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests that keep the text of the windows crisp: text under an opacity is drawn without ClearType, and layout on half
/// pixels blurs it.
/// </summary>
public class TextRenderingTests
{
    /// <summary>
    /// The XAML namespace of <c>x:Name</c>.
    /// </summary>
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// Lists the XAML files that draw DeskBuddy's own interface, relative to the application project.
    /// </summary>
    /// <returns>
    /// One entry for each XAML file under <c>Windows</c> and <c>Themes</c>.
    /// </returns>
    public static TheoryData<string> MarkupFiles()
    {
        var data = new TheoryData<string>();
        foreach (var folder in new[] { "Windows", "Themes" })
        {
            foreach (var file in Directory.EnumerateFiles(RepositoryPaths.Combine("src", "DeskBuddy.App", folder), "*.xaml"))
            {
                data.Add(Path.GetRelativePath(RepositoryPaths.Combine("src", "DeskBuddy.App"), file));
            }
        }

        return data;
    }

    /// <summary>
    /// Only pictures are dimmed with <c>Opacity</c>, whether it is written on the element, set by a style or set by a
    /// trigger of a template. Anything that may hold text is greyed through its color, as a dimmed text loses its ClearType
    /// rendering.
    /// </summary>
    /// <param name="file">
    /// The XAML file, relative to the application project.
    /// </param>
    [Theory]
    [MemberData(nameof(MarkupFiles))]
    public void Markup_Opacity_IsOnlyUsedOnImages(string file)
    {
        // Arrange
        var document = XDocument.Load(RepositoryPaths.Combine("src", "DeskBuddy.App", file));
        var imageNames = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Image")
            .Select(element => (string?)element.Attribute(Xaml + "Name"))
            .OfType<string>()
            .ToHashSet();

        // Act
        var offenders = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Setter"
                ? (string?)element.Attribute("Property") == "Opacity" && !SetsAnImage(element, imageNames)
                : element.Attribute("Opacity") is not null && element.Name.LocalName != "Image")
            .Select(element => element.ToString().Split('\n')[0])
            .ToList();

        // Assert
        offenders.Should().BeEmpty();
    }

    /// <summary>
    /// The windows lay out on whole pixels, so their text is not blurred by half-pixel positions.
    /// </summary>
    /// <param name="file">
    /// The window's XAML file under <c>Windows</c>.
    /// </param>
    [Theory]
    [InlineData("PanelWindow.xaml")]
    [InlineData("AboutWindow.xaml")]
    public void Markup_Windows_SnapToWholePixels(string file)
    {
        // Arrange
        var window = XDocument.Load(RepositoryPaths.Combine("src", "DeskBuddy.App", "Windows", file)).Root!;

        // Assert
        ((string?)window.Attribute("UseLayoutRounding")).Should().Be("True");
        ((string?)window.Attribute("SnapsToDevicePixels")).Should().Be("True");
    }

    /// <summary>
    /// Tells whether a setter dims a picture: it is in a style for images, or names an image of the same file as its
    /// target.
    /// </summary>
    /// <param name="setter">
    /// The setter.
    /// </param>
    /// <param name="imageNames">
    /// The names of the images in the file.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the setter is for a picture.
    /// </returns>
    private static bool SetsAnImage(XElement setter, HashSet<string> imageNames)
    {
        if ((string?)setter.Attribute("TargetName") is { } target)
        {
            return imageNames.Contains(target);
        }

        var style = setter.Ancestors().FirstOrDefault(element => element.Name.LocalName == "Style");
        return (string?)style?.Attribute("TargetType") == "Image";
    }
}
