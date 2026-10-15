using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.App.Rendering;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PetFrameCache.CreateAsync"/>.
/// </summary>
public class PetFrameCacheTests
{
    /// <summary>
    /// The pictures of a pet come out frozen, with a mask each, so that the thread that asked can use them although they
    /// were decoded on another one.
    /// </summary>
    [Fact]
    public async Task CreateAsync_PetWithPictures_DecodesFrozenFramesAndMasks()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-frames");
        try
        {
            var path = Path.Combine(directory.FullName, "idle.png");
            StaThread.Run(() => WritePicture(path));
            var pet = new Pet("test", 2, 2, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = new PetAnimation(4, [path]) });

            // Act
            var cache = await PetFrameCache.CreateAsync(pet);

            // Assert
            cache.Get(path).IsFrozen.Should().BeTrue();
            cache.GetMask(path).Should().NotBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A frame whose area is not inside the atlas (the file was replaced by a smaller one since the pet was loaded) fails the
    /// task with an error that counts as a pet that cannot be used, so switching to the pet shows it as broken instead of
    /// crashing.
    /// </summary>
    [Fact]
    public async Task CreateAsync_AreaOutsideAtlas_FailsWithPetFailure()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-outside");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            var frame = FrameId.Create(atlas, 100, 0, 2, 2);
            var pet = new Pet("test", 2, 2, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = new PetAnimation(4, [frame]) });

            // Act
            Exception? failure = null;
            try
            {
                await PetFrameCache.CreateAsync(pet);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            // Assert
            failure.Should().NotBeNull();
            PetLoadFailure.Is(failure!).Should().BeTrue();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A picture that is not there fails the task with an error that counts as a pet that cannot be used.
    /// </summary>
    [Fact]
    public async Task CreateAsync_MissingPicture_FailsWithPetFailure()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nothing.png");
        var pet = new Pet("test", 2, 2, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = new PetAnimation(4, [path]) });

        // Act
        Exception? failure = null;
        try
        {
            await PetFrameCache.CreateAsync(pet);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        // Assert
        failure.Should().NotBeNull();
        PetLoadFailure.Is(failure!).Should().BeTrue();
    }

    /// <summary>
    /// The frames of a pet that are cells of an atlas come out as the areas of the one image, each with its own mask.
    /// </summary>
    [Fact]
    public async Task CreateAsync_AtlasFrames_AreCutOutOfTheAtlas()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-atlas");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            var red = FrameId.Create(atlas, 0, 0, 2, 2);
            var blue = FrameId.Create(atlas, 2, 0, 2, 2);
            var pet = new Pet("test", 2, 2, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = new PetAnimation(4, [red, blue]) });

            // Act
            var cache = await PetFrameCache.CreateAsync(pet);

            // Assert
            cache.Get(red).IsFrozen.Should().BeTrue();
            cache.Get(red).Should().NotBeOfType<CroppedBitmap>("a frame must not keep the whole atlas alive");
            ((BitmapSource)cache.Get(red)).PixelWidth.Should().Be(2);
            ((BitmapSource)cache.Get(blue)).PixelHeight.Should().Be(2);
            StaThread.Run(() =>
            {
                FirstPixel(cache.Get(red)).Should().Be((0, 0, 255));
                FirstPixel(cache.Get(blue)).Should().Be((255, 0, 0));
            });
            cache.GetMask(blue).Should().NotBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The atlas image that is not there fails the task like a missing picture.
    /// </summary>
    [Fact]
    public async Task CreateAsync_AtlasMissing_FailsWithPetFailure()
    {
        // Arrange
        var frame = FrameId.Create(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "atlas.png"), 0, 0, 2, 2);
        var pet = new Pet("test", 2, 2, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = new PetAnimation(4, [frame]) });

        // Act
        Exception? failure = null;
        try
        {
            await PetFrameCache.CreateAsync(pet);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        // Assert
        failure.Should().NotBeNull();
        PetLoadFailure.Is(failure!).Should().BeTrue();
    }

    /// <summary>
    /// Writes a small picture file.
    /// </summary>
    /// <param name="path">
    /// Where to write it.
    /// </param>
    private static void WritePicture(string path)
    {
        var pixels = new byte[2 * 2 * 4];
        Array.Fill(pixels, (byte)255);
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 2 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>
    /// Writes an atlas of two cells of 2x2 pixels side by side: the left one red, the right one blue.
    /// </summary>
    /// <param name="path">
    /// Where to write it.
    /// </param>
    private static void WriteAtlas(string path)
    {
        var pixels = new byte[4 * 2 * 4];
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var offset = ((y * 4) + x) * 4;
                pixels[offset] = (byte)(x < 2 ? 0 : 255);
                pixels[offset + 2] = (byte)(x < 2 ? 255 : 0);
                pixels[offset + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(4, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 4 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>
    /// Reads the color of the first pixel of a picture.
    /// </summary>
    /// <param name="picture">
    /// The picture.
    /// </param>
    /// <returns>
    /// The blue, green and red values.
    /// </returns>
    private static (byte Blue, byte Green, byte Red) FirstPixel(ImageSource picture)
    {
        var converted = new FormatConvertedBitmap((BitmapSource)picture, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4];
        converted.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixel, 4, 0);
        return (pixel[0], pixel[1], pixel[2]);
    }
}
