using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.App.Rendering;
using DeskBuddy.Domain.Pets;
using DeskBuddy.App.ViewModels;
using DeskBuddy.Infrastructure.Pets;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PetPreviewFrames"/>, and for the pets tab over the pets shipped in the repository.
/// </summary>
public class PetPreviewFramesTests
{
    /// <summary>
    /// A picture file (the icon of a shipped pet) is decoded to a frozen image, and asking again gives the same one.
    /// </summary>
    [Fact]
    public void Get_ShippedPicture_DecodesOnceToFrozenImage()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var frames = new PetPreviewFrames();
            var path = Path.Combine(RepositoryPaths.Pets, "robot", "icon.png");

            // Act
            var first = frames.Get(path);
            var second = frames.Get(path);

            // Assert
            first.Should().NotBeNull();
            first!.IsFrozen.Should().BeTrue();
            second.Should().BeSameAs(first);
        });
    }

    /// <summary>
    /// A file that is not there leaves a gap in the preview instead of failing.
    /// </summary>
    [Fact]
    public void Get_MissingFile_ReturnsNull()
    {
        StaThread.Run(() =>
        {
            // Act
            var image = new PetPreviewFrames().Get(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nothing.png"));

            // Assert
            image.Should().BeNull();
        });
    }

    /// <summary>
    /// A picture that is an area of an atlas is cut out of it, and the atlas is decoded only once.
    /// </summary>
    [Fact]
    public void Get_AtlasCell_CutsTheAreaOutOfTheAtlas()
    {
        var directory = Directory.CreateTempSubdirectory("deskbuddy-preview");
        try
        {
            StaThread.Run(() =>
            {
                // Arrange
                var atlas = Path.Combine(directory.FullName, "atlas.png");
                WriteAtlas(atlas);
                using var frames = new PetPreviewFrames();

                // Act
                var red = frames.Get(FrameId.Create(atlas, 0, 0, 2, 2));
                var blue = frames.Get(FrameId.Create(atlas, 2, 0, 2, 2));
                var again = frames.Get(FrameId.Create(atlas, 2, 0, 2, 2));

                // Assert
                red.Should().NotBeNull();
                ((BitmapSource)red!).PixelWidth.Should().Be(2);
                FirstPixel(red).Should().Be((0, 0, 255));
                FirstPixel(blue!).Should().Be((255, 0, 0));
                again.Should().BeSameAs(blue);
            });
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// An area outside the atlas leaves a gap instead of failing.
    /// </summary>
    [Fact]
    public void Get_AtlasCellOutsideImage_ReturnsNull()
    {
        var directory = Directory.CreateTempSubdirectory("deskbuddy-preview");
        try
        {
            StaThread.Run(() =>
            {
                // Arrange
                var atlas = Path.Combine(directory.FullName, "atlas.png");
                WriteAtlas(atlas);

                // Act
                var picture = new PetPreviewFrames().Get(FrameId.Create(atlas, 4, 0, 2, 2));

                // Assert
                picture.Should().BeNull();
            });
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Pictures preloaded on another thread are ready to be had without the file: the atlas is deleted after the preload and
    /// the pictures are still there, frozen so that the UI thread can use them.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_AtlasCells_ArePreparedBeforeGet()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-preload");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            var red = FrameId.Create(atlas, 0, 0, 2, 2);
            var blue = FrameId.Create(atlas, 2, 0, 2, 2);
            using var frames = new PetPreviewFrames();

            // Act
            await frames.PreloadAsync([red, blue, red]);
            File.Delete(atlas);

            // Assert
            var redPicture = frames.Get(red);
            var bluePicture = frames.Get(blue);
            redPicture.Should().NotBeNull();
            redPicture!.IsFrozen.Should().BeTrue();
            StaThread.Run(() =>
            {
                FirstPixel(redPicture).Should().Be((0, 0, 255));
                FirstPixel(bluePicture!).Should().Be((255, 0, 0));
            });
            frames.Get(red).Should().BeSameAs(redPicture);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A picture that cannot be read does not fail the preload; it is left as a gap, as <c>Get</c> leaves it.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_MissingAtlas_CompletesAndLeavesAGap()
    {
        // Arrange
        var missing = FrameId.Create(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "atlas.png"), 0, 0, 2, 2);
        using var frames = new PetPreviewFrames();

        // Act
        await frames.PreloadAsync([missing]);

        // Assert
        frames.Get(missing).Should().BeNull();
    }

    /// <summary>
    /// Pictures are used from the thread that preloads and from another at the same time without trouble.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_WhileGetOnAnotherThread_BothWork()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-preload");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            var red = FrameId.Create(atlas, 0, 0, 2, 2);
            var blue = FrameId.Create(atlas, 2, 0, 2, 2);
            using var frames = new PetPreviewFrames();

            // Act
            var preloading = frames.PreloadAsync([red, blue]);
            ImageSource? fromHere = null;
            StaThread.Run(() => fromHere = frames.Get(blue));
            await preloading;

            // Assert
            fromHere.Should().NotBeNull();
            frames.Get(red).Should().NotBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Asking for pictures that are all prepared already completes at once, without waking the worker.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_AllAlreadyPrepared_CompletesAtOnce()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-preload");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            var red = FrameId.Create(atlas, 0, 0, 2, 2);
            using var frames = new PetPreviewFrames();
            await frames.PreloadAsync([red]);

            // Act
            var again = frames.PreloadAsync([red]);

            // Assert
            again.IsCompletedSuccessfully.Should().BeTrue();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// However many threads ask for a picture at the same time (the UI thread while the worker is cutting it out), they all
    /// get the same picture.
    /// </summary>
    [Fact]
    public async Task Get_ManyThreadsAtOnce_AllGetTheSamePicture()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-preload");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            var red = FrameId.Create(atlas, 0, 0, 2, 2);

            for (var round = 0; round < 10; round++)
            {
                using var frames = new PetPreviewFrames();

                // Act
                var preloading = frames.PreloadAsync([red]);
                var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
                {
                    ImageSource? picture = null;
                    StaThread.Run(() => picture = frames.Get(red));
                    return picture;
                })).ToList();
                var pictures = await Task.WhenAll(readers);
                await preloading;

                // Assert
                pictures.Should().OnlyContain(picture => picture != null);
                pictures.Distinct().Should().HaveCount(1);
                frames.Get(red).Should().BeSameAs(pictures[0]);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// An image file that cannot be decoded is remembered as unreadable while previews ask for its pictures, so the other
    /// pictures of it are not tried again at every frame.
    /// </summary>
    [Fact]
    public void Get_UnreadableAtlas_StaysAGapForAllItsCells()
    {
        var directory = Directory.CreateTempSubdirectory("deskbuddy-unreadable");
        try
        {
            StaThread.Run(() =>
            {
                // Arrange
                var atlas = Path.Combine(directory.FullName, "atlas.png");
                File.WriteAllBytes(atlas, [1, 2, 3, 4]);
                using var frames = new PetPreviewFrames();

                // Act
                var first = frames.Get(FrameId.Create(atlas, 0, 0, 2, 2));
                WriteAtlas(atlas);
                var second = frames.Get(FrameId.Create(atlas, 2, 0, 2, 2));

                // Assert
                first.Should().BeNull();
                second.Should().BeNull();
            });
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A preload tries again what failed before, so a pet whose atlas has been repaired is picked up when its pets are
    /// listed again, without restarting the application.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_AtlasRepairedAfterFailure_PicksItUp()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-repaired");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            File.WriteAllBytes(atlas, [1, 2, 3, 4]);
            var red = FrameId.Create(atlas, 0, 0, 2, 2);
            var blue = FrameId.Create(atlas, 2, 0, 2, 2);
            using var frames = new PetPreviewFrames();
            await frames.PreloadAsync([red, blue]);
            frames.Get(red).Should().BeNull();
            StaThread.Run(() => WriteAtlas(atlas));

            // Act
            await frames.PreloadAsync([red, blue]);

            // Assert
            frames.Get(red).Should().NotBeNull();
            frames.Get(blue).Should().NotBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A file that cannot be opened (here, one that is locked) leaves a gap on whichever thread asks, instead of failing.
    /// </summary>
    [Fact]
    public async Task Get_LockedAtlas_LeavesAGapOnBothThreads()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-locked");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            await using var locked = new FileStream(atlas, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var frames = new PetPreviewFrames();
            var cell = FrameId.Create(atlas, 0, 0, 2, 2);
            var other = FrameId.Create(atlas, 2, 0, 2, 2);

            // Act
            await frames.PreloadAsync([cell]);
            ImageSource? fromUiThread = null;
            StaThread.Run(() => fromUiThread = frames.Get(other));

            // Assert
            frames.Get(cell).Should().BeNull();
            fromUiThread.Should().BeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A picture that was cut out wins over a failure that was remembered for the same name, in whichever order the two come,
    /// and a failure never replaces a picture.
    /// </summary>
    [Fact]
    public void Remember_PictureAndFailure_PictureAlwaysWins()
    {
        // Arrange
        var picture = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        picture.Freeze();
        using var frames = new PetPreviewFrames();

        // Act
        var failureFirst = frames.Remember("a.png#0,0,1,1", null);
        var thenPicture = frames.Remember("a.png#0,0,1,1", picture);
        var pictureFirst = frames.Remember("b.png#0,0,1,1", picture);
        var thenFailure = frames.Remember("b.png#0,0,1,1", null);

        // Assert
        failureFirst.Should().BeNull();
        thenPicture.Should().BeSameAs(picture);
        frames.Get("a.png#0,0,1,1").Should().BeSameAs(picture);
        pictureFirst.Should().BeSameAs(picture);
        thenFailure.Should().BeSameAs(picture);
        frames.Get("b.png#0,0,1,1").Should().BeSameAs(picture);
    }

    /// <summary>
    /// The failures of reading a file that make a pet unusable (including a file that may not be read) leave a gap in a
    /// preview, and so does the one WPF raises for an image it cannot make sense of; a bug does not.
    /// </summary>
    /// <param name="exceptionType">
    /// The type of the exception.
    /// </param>
    /// <param name="isUnreadable">
    /// Whether it counts as a file that cannot be read.
    /// </param>
    [Theory]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(NotSupportedException), true)]
    [InlineData(typeof(FormatException), true)]
    [InlineData(typeof(InvalidDataException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(NullReferenceException), false)]
    [InlineData(typeof(OutOfMemoryException), false)]
    public void IsUnreadable_Exception_TellsAFileFailureFromABug(Type exceptionType, bool isUnreadable)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        // Act
        var result = PetPreviewFrames.IsUnreadable(exception);

        // Assert
        result.Should().Be(isUnreadable);
    }

    /// <summary>
    /// While a request cuts out many pictures of one atlas the atlas is decoded once, even if the garbage collector runs all
    /// the time meanwhile, because the worker holds it until the request is done.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_ManyCellsOfOneAtlas_DecodesTheAtlasOnce()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-hold");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteGrid(atlas, columns: 10, rows: 10));
            var decodes = 0;
            using var frames = new PetPreviewFrames(path =>
            {
                Interlocked.Increment(ref decodes);
                return FrameImages.Load(path);
            });
            var cells = Enumerable.Range(0, 100).Select(cell => FrameId.Create(atlas, (cell % 10) * 2, (cell / 10) * 2, 2, 2)).ToList();
            using var stop = new CancellationTokenSource();
            var collector = Task.Run(
                () =>
                {
                    // Bounded, so that the test cannot run long however slow the machine is.
                    for (var round = 0; round < 500 && !stop.IsCancellationRequested; round++)
                    {
                        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                        Thread.Sleep(1);
                    }
                },
                CancellationToken.None);

            // Act
            await frames.PreloadAsync(cells);
            await stop.CancelAsync();
            await collector;

            // Assert
            decodes.Should().Be(1);
            cells.Should().OnlyContain(cell => frames.Get(cell) != null);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Two threads that need pictures of the same atlas at the same time decode it once: the second waits for the first.
    /// The second thread is started once the first is inside the decoder, which is held until the second is on its way.
    /// </summary>
    [Fact]
    public async Task Get_TwoThreadsNeedingTheSameAtlas_DecodeItOnce()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-gate");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            var decodes = 0;
            BitmapSource? kept = null;
            using var inDecoder = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var frames = new PetPreviewFrames(path =>
            {
                Interlocked.Increment(ref decodes);
                inDecoder.Set();
                release.Wait(TimeSpan.FromSeconds(10));
                kept = FrameImages.Load(path);
                return kept;
            });

            // Act
            var first = Task.Run(() =>
            {
                ImageSource? picture = null;
                StaThread.Run(() => picture = frames.Get(FrameId.Create(atlas, 0, 0, 2, 2)));
                return picture;
            });
            inDecoder.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            var second = Task.Run(() =>
            {
                ImageSource? picture = null;
                StaThread.Run(() => picture = frames.Get(FrameId.Create(atlas, 2, 0, 2, 2)));
                return picture;
            });
            await Task.Delay(100, TestContext.Current.CancellationToken);
            release.Set();
            var pictures = await Task.WhenAll(first, second);

            // Assert
            decodes.Should().Be(1);
            pictures.Should().OnlyContain(picture => picture != null);
            GC.KeepAlive(kept);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A file that was marked unreadable because of one picture is tried again when other pictures of it are asked for after
    /// it has been repaired, not only the picture that failed.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_OtherCellsAfterRepair_AreCutOut()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-repaired-other");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            File.WriteAllBytes(atlas, [1, 2, 3, 4]);
            var red = FrameId.Create(atlas, 0, 0, 2, 2);
            var blue = FrameId.Create(atlas, 2, 0, 2, 2);
            using var frames = new PetPreviewFrames();
            await frames.PreloadAsync([red]);
            frames.Get(red).Should().BeNull();
            StaThread.Run(() => WriteAtlas(atlas));

            // Act
            await frames.PreloadAsync([blue]);

            // Assert
            frames.Get(blue).Should().NotBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Disposing waits for the request the worker is on, so nothing is still decoding when the application goes on to shut
    /// down.
    /// </summary>
    [Fact]
    public async Task Dispose_WorkerBusy_WaitsForItsRequest()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-dispose");
        try
        {
            var atlas = Path.Combine(directory.FullName, "atlas.png");
            StaThread.Run(() => WriteAtlas(atlas));
            using var inDecoder = new ManualResetEventSlim();
            var frames = new PetPreviewFrames(path =>
            {
                inDecoder.Set();
                Thread.Sleep(300);
                return FrameImages.Load(path);
            });
            var preloading = frames.PreloadAsync([FrameId.Create(atlas, 0, 0, 2, 2)]);
            inDecoder.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

            // Act
            frames.Dispose();

            // Assert
            preloading.IsCompleted.Should().BeTrue();
            await preloading;
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Disposing while requests are queued lets the worker finish the picture it is on and skip the rest, completing the
    /// requests, so the application does not wait for pictures nothing needs any more.
    /// </summary>
    [Fact]
    public async Task Dispose_RequestsQueued_SkipsTheRest()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("deskbuddy-skip");
        try
        {
            var first = Path.Combine(directory.FullName, "first.png");
            var second = Path.Combine(directory.FullName, "second.png");
            StaThread.Run(() =>
            {
                WriteAtlas(first);
                WriteAtlas(second);
            });
            var decodes = 0;
            using var inDecoder = new ManualResetEventSlim();
            var frames = new PetPreviewFrames(path =>
            {
                Interlocked.Increment(ref decodes);
                inDecoder.Set();
                Thread.Sleep(300);
                return FrameImages.Load(path);
            });
            var firstRequest = frames.PreloadAsync([FrameId.Create(first, 0, 0, 2, 2)]);
            inDecoder.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            var secondRequest = frames.PreloadAsync([FrameId.Create(second, 0, 0, 2, 2)]);

            // Act
            frames.Dispose();

            // Assert
            await Task.WhenAll(firstRequest, secondRequest);
            decodes.Should().Be(1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Disposing twice, and disposing a cache whose worker never started, do nothing the second time and do not fail.
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotFail()
    {
        // Arrange
        var frames = new PetPreviewFrames();

        // Act
        frames.Dispose();
        var act = frames.Dispose;

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// After the cache is disposed a preload does nothing and does not fail, and pictures can still be had.
    /// </summary>
    [Fact]
    public async Task PreloadAsync_AfterDispose_CompletesWithoutPreparing()
    {
        // Arrange
        var frames = new PetPreviewFrames();
        frames.Dispose();

        // Act
        var act = () => frames.PreloadAsync(["nothing.png"]);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// The pets tab lists the shipped pets and every preview has a real picture, whichever state it is showing.
    /// </summary>
    [Fact]
    public void PetsTab_ShippedPets_EveryPreviewShowsPictureInEveryState()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var petsDirectory = RepositoryPaths.Pets;
            var settings = new SettingsService(new FakeSettingsStore());
            settings.InitializeAsync().GetAwaiter().GetResult();
            var loader = new FolderPetLoader(NullLogger<FolderPetLoader>.Instance);
            var switcher = new PetSwitcher(
                settings,
                loader,
                petsDirectory,
                settings.Current.PetName,
                _ => { },
                NullLogger<PetSwitcher>.Instance);
            var pets = new PetsTabViewModel(
                settings,
                new FolderPetCatalog(petsDirectory),
                loader,
                petsDirectory,
                switcher,
                new PetPreviewFrames().Get);

            // Act
            pets.RefreshAsync().GetAwaiter().GetResult();

            // Assert
            pets.Cards.Select(card => card.FolderName).Should().Contain([PetSettings.DefaultPetName, "cat"]);
            pets.Cards.Should().OnlyContain(card => card.IsAvailable);
            pets.Cards.Single(card => card.IsCurrent).FolderName.Should().Be(PetSettings.DefaultPetName);
            for (var step = 0; step < 12; step++)
            {
                pets.Cards.Should().OnlyContain(card => card.CurrentFrame != null);
                pets.Tick(PetCardViewModel.StateDuration);
            }
        });
    }

    /// <summary>
    /// Writes an atlas of cells of 2x2 pixels in a grid, every pixel opaque.
    /// </summary>
    /// <param name="path">
    /// Where to write it.
    /// </param>
    /// <param name="columns">
    /// The number of cells in a row.
    /// </param>
    /// <param name="rows">
    /// The number of rows of cells.
    /// </param>
    private static void WriteGrid(string path, int columns, int rows)
    {
        var width = columns * 2;
        var height = rows * 2;
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, (byte)200);
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
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
