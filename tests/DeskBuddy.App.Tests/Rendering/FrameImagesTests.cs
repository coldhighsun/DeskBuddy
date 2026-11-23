using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.App.Rendering;

namespace DeskBuddy.App.Tests.Rendering;

/// <summary>
/// Tests for <see cref="FrameImages.Crop"/>, built on bitmaps made in memory.
/// </summary>
public class FrameImagesTests
{
    /// <summary>
    /// An area larger than the limit is scaled down to it, keeping its proportions, whatever they are.
    /// </summary>
    [Theory]
    [InlineData(400, 200, 100, 100, 50)]
    [InlineData(200, 400, 100, 50, 100)]
    [InlineData(256, 256, 160, 160, 160)]
    public void Crop_AreaLargerThanMaxEdge_ScalesDownKeepingProportions(
        int width,
        int height,
        int maxEdge,
        int expectedWidth,
        int expectedHeight)
    {
        StaThread.Run(() =>
        {
            // Arrange
            var image = Solid(width + 10, height + 10, 255, 0, 0, 255);

            // Act
            var picture = FrameImages.Crop(image, (5, 5, width, height), maxEdge);

            // Assert
            picture.PixelWidth.Should().Be(expectedWidth);
            picture.PixelHeight.Should().Be(expectedHeight);
            picture.IsFrozen.Should().BeTrue();
        });
    }

    /// <summary>
    /// An area that already fits is never scaled up.
    /// </summary>
    [Fact]
    public void Crop_AreaWithinMaxEdge_KeepsItsSize()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var image = Solid(64, 32, 255, 0, 0, 255);

            // Act
            var picture = FrameImages.Crop(image, (0, 0, 64, 32), maxEdge: 160);

            // Assert
            picture.PixelWidth.Should().Be(64);
            picture.PixelHeight.Should().Be(32);
        });
    }

    /// <summary>
    /// Without a limit the area keeps its size, which is how the pet itself reads its frames.
    /// </summary>
    [Fact]
    public void Crop_NoMaxEdge_KeepsItsSize()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var image = Solid(300, 300, 255, 0, 0, 255);

            // Act
            var picture = FrameImages.Crop(image, (0, 0, 300, 300));

            // Assert
            picture.PixelWidth.Should().Be(300);
            picture.PixelHeight.Should().Be(300);
        });
    }

    /// <summary>
    /// Scaling keeps the colors and the opacity of a solid picture, so a scaled preview does not darken or fade.
    /// </summary>
    [Fact]
    public void Crop_ScaledSolidPicture_KeepsColorAndOpacity()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var image = Solid(200, 200, 40, 90, 200, 255);

            // Act
            var picture = FrameImages.Crop(image, (0, 0, 200, 200), maxEdge: 50);

            // Assert
            var pixel = new byte[4];
            picture.CopyPixels(new System.Windows.Int32Rect(25, 25, 1, 1), pixel, 4, 0);
            pixel.Should().Equal(40, 90, 200, 255);
        });
    }

    /// <summary>
    /// A limit that is not a size is a programming error.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Crop_NonPositiveMaxEdge_Throws(int maxEdge)
    {
        StaThread.Run(() =>
        {
            // Arrange
            var image = Solid(10, 10, 0, 0, 0, 255);

            // Act
            var act = () => FrameImages.Crop(image, (0, 0, 10, 10), maxEdge);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        });
    }

    /// <summary>
    /// An area outside the image is refused, with or without a limit.
    /// </summary>
    [Fact]
    public void Crop_AreaOutsideImage_ThrowsInvalidData()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var image = Solid(10, 10, 0, 0, 0, 255);

            // Act
            var act = () => FrameImages.Crop(image, (5, 5, 10, 10), maxEdge: 4);

            // Assert
            act.Should().Throw<System.IO.InvalidDataException>();
        });
    }

    /// <summary>
    /// Makes a picture of one color.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="blue">The blue value.</param>
    /// <param name="green">The green value.</param>
    /// <param name="red">The red value.</param>
    /// <param name="alpha">The opacity.</param>
    /// <returns>The picture, in straight (not premultiplied) alpha.</returns>
    private static BitmapSource Solid(int width, int height, byte blue, byte green, byte red, byte alpha)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = blue;
            pixels[i + 1] = green;
            pixels[i + 2] = red;
            pixels[i + 3] = alpha;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
