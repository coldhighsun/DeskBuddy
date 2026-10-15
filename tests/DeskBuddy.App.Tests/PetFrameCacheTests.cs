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
}
