using AwesomeAssertions;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PixelSizeCache"/>.
/// </summary>
public sealed class PixelSizeCacheTests
{
    /// <summary>
    /// A size that was read is remembered, so the window is asked only once.
    /// </summary>
    [Fact]
    public void Get_CalledTwice_ReadsTheWindowOnce()
    {
        // Arrange
        var reads = 0;
        var cache = new PixelSizeCache(() =>
        {
            reads++;
            return (128, 128);
        });

        // Act
        var first = cache.Get();
        var second = cache.Get();

        // Assert
        first.Should().Be((128, 128));
        second.Should().Be((128, 128));
        reads.Should().Be(1);
    }

    /// <summary>
    /// A failed read, which comes out as an empty size, is not remembered: the next call reads again and gets the real
    /// size.
    /// </summary>
    [Fact]
    public void Get_AfterEmptyRead_ReadsAgain()
    {
        // Arrange
        var sizes = new Queue<(int Width, int Height)>([(0, 0), (192, 160)]);
        var cache = new PixelSizeCache(() => sizes.Dequeue());

        // Act
        var failed = cache.Get();
        var real = cache.Get();
        var remembered = cache.Get();

        // Assert
        failed.Should().Be((0, 0));
        real.Should().Be((192, 160));
        remembered.Should().Be((192, 160));
    }

    /// <summary>
    /// After the window was resized the size is read again.
    /// </summary>
    [Fact]
    public void Get_AfterInvalidate_ReadsAgain()
    {
        // Arrange
        var sizes = new Queue<(int Width, int Height)>([(128, 128), (192, 192)]);
        var cache = new PixelSizeCache(() => sizes.Dequeue());
        cache.Get();

        // Act
        cache.Invalidate();
        var size = cache.Get();

        // Assert
        size.Should().Be((192, 192));
    }
}
