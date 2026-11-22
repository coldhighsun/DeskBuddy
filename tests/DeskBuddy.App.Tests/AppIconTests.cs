using System.Buffers.Binary;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="AppIcon"/>: making the tray icon from a pet's picture.
/// </summary>
public class AppIconTests
{
    /// <summary>
    /// A pet's picture becomes an icon of the requested size, whatever the proportions of the picture.
    /// </summary>
    /// <param name="pictureWidth">The width of the picture in pixels.</param>
    /// <param name="pictureHeight">The height of the picture in pixels.</param>
    [Theory]
    [InlineData(64, 64)]
    [InlineData(64, 32)]
    [InlineData(10, 40)]
    public void FromPicture_Picture_GivesIconOfRequestedSize(int pictureWidth, int pictureHeight)
    {
        StaThread.Run(() =>
        {
            // Arrange
            var picture = SolidPicture(pictureWidth, pictureHeight, Colors.OrangeRed);

            // Act
            using var icon = AppIcon.FromPicture(picture, 24, 24);

            // Assert
            icon.Size.Should().Be(new Size(24, 24));
        });
    }

    /// <summary>
    /// The icon shows the picture: a pixel in the middle of a solid picture has the color of the picture, and the transparent
    /// edge of a picture that does not fill the icon stays transparent.
    /// </summary>
    [Fact]
    public void FromPicture_NarrowPicture_KeepsColorAndLeavesEdgesTransparent()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var picture = SolidPicture(10, 40, Colors.OrangeRed);

            // Act
            using var icon = AppIcon.FromPicture(picture, 32, 32);
            using var bitmap = icon.ToBitmap();

            // Assert
            bitmap.GetPixel(16, 16).R.Should().BeGreaterThan(200);
            bitmap.GetPixel(16, 16).A.Should().Be(255);
            bitmap.GetPixel(1, 16).A.Should().Be(0);
        });
    }

    /// <summary>
    /// The icon shows the top three quarters of the pet: the box around the pixels that are not transparent is found, and
    /// the area is as wide as the box (at most three quarters of its larger side) and starts at its top.
    /// </summary>
    [Fact]
    public void FocusArea_PetWithTransparentBorder_TakesTopThreeQuartersOfBox()
    {
        StaThread.Run(() =>
        {
            // Arrange: a 64x64 picture whose pet occupies x 10-49, y 4-63 (40 wide, 60 tall).
            var pixels = new byte[64 * 64 * 4];
            for (var y = 4; y < 64; y++)
            {
                for (var x = 10; x < 50; x++)
                {
                    pixels[(((y * 64) + x) * 4) + 3] = 255;
                }
            }

            var picture = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);

            // Act
            var area = AppIcon.FocusArea(picture);

            // Assert: the side is 75% of 60 = 45, so the whole 40 width and the top 45 rows.
            area.Should().Be(new System.Windows.Int32Rect(10, 4, 40, 45));
        });
    }

    /// <summary>
    /// A picture with nothing that is not transparent has no focus area.
    /// </summary>
    [Fact]
    public void FocusArea_FullyTransparentPicture_IsEmpty()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var picture = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, new byte[8 * 8 * 4], 8 * 4);

            // Act
            var area = AppIcon.FocusArea(picture);

            // Assert
            area.IsEmpty.Should().BeTrue();
        });
    }

    /// <summary>
    /// Makes a picture that is one color all over.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="color">The color.</param>
    /// <returns>The frozen picture.</returns>
    private static BitmapSource SolidPicture(int width, int height, System.Windows.Media.Color color)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 255;
        }

        var picture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        picture.Freeze();
        return picture;
    }
}
