using System.Buffers.Binary;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for the application icon file that the tray, the executable and the installer use.
/// </summary>
public class AppIconTests
{
    /// <summary>
    /// The PNG file signature.
    /// </summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// The icon file is a valid icon directory.
    /// </summary>
    [Fact]
    public void Icon_File_HasIconDirectoryHeader()
    {
        // Act
        var bytes = ReadIcon();

        // Assert
        BitConverter.ToUInt16(bytes, 0).Should().Be(0);
        BitConverter.ToUInt16(bytes, 2).Should().Be(1);
        BitConverter.ToUInt16(bytes, 4).Should().BeGreaterThan(0);
    }

    /// <summary>
    /// The sizes Windows asks for (tray, taskbar, Explorer, high DPI) are all in the icon.
    /// </summary>
    [Fact]
    public void Icon_File_ContainsTheCommonSizes()
    {
        // Act
        var sizes = ReadEntries(ReadIcon()).Select(entry => entry.Size);

        // Assert
        sizes.Should().Contain([16, 32, 48, 256]);
    }

    /// <summary>
    /// Every image in the icon is a PNG that lies inside the file.
    /// </summary>
    [Fact]
    public void Icon_File_EveryImageIsAPng()
    {
        // Arrange
        var bytes = ReadIcon();

        // Act
        var entries = ReadEntries(bytes);

        // Assert
        foreach (var entry in entries)
        {
            (entry.Offset + entry.Length).Should().BeLessThanOrEqualTo(bytes.Length);
            bytes.AsSpan(entry.Offset, PngSignature.Length).SequenceEqual(PngSignature).Should().BeTrue();
        }
    }

    /// <summary>
    /// The size a directory entry announces is the size of the PNG it points to.
    /// </summary>
    [Fact]
    public void Icon_File_EntrySizesMatchThePngDimensions()
    {
        // Arrange
        var bytes = ReadIcon();

        // Act
        var mismatches = ReadEntries(bytes)
            .Where(entry => PngSize(bytes, entry.Offset) != (entry.Size, entry.Size))
            .ToList();

        // Assert
        mismatches.Should().BeEmpty();
    }

    /// <summary>
    /// The file loads as an icon at the sizes of the notification area and the taskbar, and gets the frame of that size.
    /// </summary>
    /// <param name="size">
    /// The requested size in pixels.
    /// </param>
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void Icon_File_LoadsAtRequestedSize(int size)
    {
        // Arrange
        using var stream = new MemoryStream(ReadIcon());

        // Act
        using var icon = new Icon(stream, new Size(size, size));

        // Assert
        icon.Size.Should().Be(new Size(size, size));
    }

    /// <summary>
    /// A pet's picture becomes an icon of the requested size, whatever the proportions of the picture.
    /// </summary>
    /// <param name="pictureWidth">
    /// The width of the picture in pixels.
    /// </param>
    /// <param name="pictureHeight">
    /// The height of the picture in pixels.
    /// </param>
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
    /// <param name="width">
    /// The width in pixels.
    /// </param>
    /// <param name="height">
    /// The height in pixels.
    /// </param>
    /// <param name="color">
    /// The color.
    /// </param>
    /// <returns>
    /// The frozen picture.
    /// </returns>
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

    /// <summary>
    /// Reads the width and height from the header chunk of a PNG.
    /// </summary>
    /// <param name="bytes">
    /// The file that holds the PNG.
    /// </param>
    /// <param name="offset">
    /// Where the PNG starts.
    /// </param>
    /// <returns>
    /// The width and height in pixels.
    /// </returns>
    private static (int Width, int Height) PngSize(byte[] bytes, int offset)
    {
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 20, 4));
        return (width, height);
    }

    /// <summary>
    /// Reads the icon directory entries: the pixel size, and where the image is and how long.
    /// </summary>
    /// <param name="bytes">
    /// The icon file.
    /// </param>
    /// <returns>
    /// The entries.
    /// </returns>
    private static List<(int Size, int Offset, int Length)> ReadEntries(byte[] bytes)
    {
        var count = BitConverter.ToUInt16(bytes, 4);
        var entries = new List<(int Size, int Offset, int Length)>();
        for (var i = 0; i < count; i++)
        {
            var position = 6 + (16 * i);
            var size = bytes[position] == 0 ? 256 : bytes[position];
            entries.Add((size, BitConverter.ToInt32(bytes, position + 12), BitConverter.ToInt32(bytes, position + 8)));
        }

        return entries;
    }

    /// <summary>
    /// Reads the icon file of the application project.
    /// </summary>
    /// <returns>
    /// The file's bytes.
    /// </returns>
    private static byte[] ReadIcon()
    {
        return File.ReadAllBytes(RepositoryPaths.Combine("src", "DeskBuddy.App", "Assets", "DeskBuddy.ico"));
    }
}
