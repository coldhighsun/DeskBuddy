using System.Buffers.Binary;
using System.Drawing;
using System.IO;

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
