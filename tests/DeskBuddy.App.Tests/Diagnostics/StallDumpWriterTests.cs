using System.IO;
using DeskBuddy.App.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Win32.SafeHandles;

namespace DeskBuddy.App.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="StallDumpWriter"/>. The dump itself is faked, so no dump of the test process is taken.
/// </summary>
public sealed class StallDumpWriterTests : IDisposable
{
    /// <summary>
    /// A folder of its own for each test, removed afterwards.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"DeskBuddyDumps-{Guid.NewGuid():N}");

    /// <summary>
    /// The clock that names the dumps.
    /// </summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 9, 50, 0, TimeSpan.Zero));

    /// <summary>
    /// Removes the folder of the test.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// A dump is written to the folder, which is made if it is missing.
    /// </summary>
    [Fact]
    public void TryWrite_MissingFolder_CreatesItAndWritesTheDump()
    {
        // Arrange
        var folder = Path.Combine(_root, "dumps");
        var writer = new StallDumpWriter(folder, 3, _time, NullLogger.Instance, WriteBytes, TimeSpan.Zero);

        // Act
        var path = writer.TryWrite();

        // Assert
        path.Should().NotBeNull();
        Path.GetDirectoryName(path).Should().Be(folder);
        File.ReadAllBytes(path!).Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// Only the newest dumps are kept.
    /// </summary>
    [Fact]
    public void TryWrite_MoreThanKept_RemovesTheOldest()
    {
        // Arrange
        var writer = new StallDumpWriter(_root, 2, _time, NullLogger.Instance, WriteBytes, TimeSpan.Zero);
        var paths = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            paths.Add(writer.TryWrite()!);
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        // Act
        var left = Directory.GetFiles(_root);

        // Assert
        left.Should().BeEquivalentTo(paths.Skip(2));
    }

    /// <summary>
    /// Files that are not dumps of the watchdog are left alone, however many there are.
    /// </summary>
    [Fact]
    public void TryWrite_OtherFilesInTheFolder_AreNotRemoved()
    {
        // Arrange
        Directory.CreateDirectory(_root);
        var other = Path.Combine(_root, "notes.txt");
        File.WriteAllText(other, "keep me");
        var writer = new StallDumpWriter(_root, 1, _time, NullLogger.Instance, WriteBytes, TimeSpan.Zero);

        // Act
        writer.TryWrite();
        _time.Advance(TimeSpan.FromSeconds(1));
        writer.TryWrite();

        // Assert
        File.Exists(other).Should().BeTrue();
        Directory.GetFiles(_root, "*.dmp").Should().HaveCount(1);
    }

    /// <summary>
    /// A dump that Windows could not write leaves no half file and no error.
    /// </summary>
    [Fact]
    public void TryWrite_DumpFails_ReturnsNullAndLeavesNoFile()
    {
        // Arrange
        var writer = new StallDumpWriter(_root, 3, _time, NullLogger.Instance, _ => false, TimeSpan.Zero);

        // Act
        var path = writer.TryWrite();

        // Assert
        path.Should().BeNull();
        Directory.GetFiles(_root).Should().BeEmpty();
    }

    /// <summary>
    /// A folder that cannot be made is a logged failure, not an exception.
    /// </summary>
    [Fact]
    public void TryWrite_FolderCannotBeMade_ReturnsNull()
    {
        // Arrange
        Directory.CreateDirectory(_root);
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "a file where a folder is wanted");
        var writer = new StallDumpWriter(Path.Combine(blocker, "dumps"), 3, _time, NullLogger.Instance, WriteBytes, TimeSpan.Zero);

        // Act
        var path = writer.TryWrite();

        // Assert
        path.Should().BeNull();
    }

    /// <summary>
    /// Two dumps at the very same moment do not overwrite each other: the second is refused, not thrown.
    /// </summary>
    [Fact]
    public void TryWrite_SameInstantTwice_KeepsTheFirstAndReturnsNull()
    {
        // Arrange
        var writer = new StallDumpWriter(_root, 3, _time, NullLogger.Instance, WriteBytes, TimeSpan.Zero);
        var first = writer.TryWrite();

        // Act
        var second = writer.TryWrite();

        // Assert
        first.Should().NotBeNull();
        second.Should().BeNull();
        File.Exists(first).Should().BeTrue();
    }

    /// <summary>
    /// A request that comes soon after a dump was written is given that dump, and no second dump is taken: the second of
    /// two stalls that begin together has nothing to add to it.
    /// </summary>
    [Fact]
    public void TryWrite_SoonAfterADump_ReturnsTheSameDumpWithoutWritingAnother()
    {
        // Arrange
        var writes = 0;
        var writer = new StallDumpWriter(
            _root,
            3,
            _time,
            NullLogger.Instance,
            handle =>
            {
                writes++;
                return WriteBytes(handle);
            });
        var first = writer.TryWrite();
        _time.Advance(StallDumpWriter.DefaultMinGap - TimeSpan.FromMilliseconds(1));

        // Act
        var second = writer.TryWrite();

        // Assert
        second.Should().Be(first);
        writes.Should().Be(1);
        Directory.GetFiles(_root).Should().HaveCount(1);
    }

    /// <summary>
    /// A stall that comes once the gap has passed gets a dump of its own.
    /// </summary>
    [Fact]
    public void TryWrite_AfterTheGap_WritesANewDump()
    {
        // Arrange
        var writer = new StallDumpWriter(_root, 3, _time, NullLogger.Instance, WriteBytes);
        var first = writer.TryWrite();
        _time.Advance(StallDumpWriter.DefaultMinGap);

        // Act
        var second = writer.TryWrite();

        // Assert
        second.Should().NotBeNull().And.NotBe(first);
        Directory.GetFiles(_root).Should().HaveCount(2);
    }

    /// <summary>
    /// A dump that failed does not count as one: the next request tries again at once.
    /// </summary>
    [Fact]
    public void TryWrite_AfterAFailedDump_TriesAgain()
    {
        // Arrange
        var succeed = false;
        var writer = new StallDumpWriter(
            _root,
            3,
            _time,
            NullLogger.Instance,
            handle => succeed && WriteBytes(handle));
        writer.TryWrite().Should().BeNull();
        succeed = true;

        // Act
        var path = writer.TryWrite();

        // Assert
        path.Should().NotBeNull();
    }

    /// <summary>
    /// The two watchdogs call from threads of their own; the dump functions of Windows are single-threaded, so the writes
    /// must never overlap.
    /// </summary>
    [Fact]
    public void TryWrite_FromManyThreadsAtOnce_NeverWritesTwoDumpsAtOnce()
    {
        // Arrange
        var active = 0;
        var mostAtOnce = 0;
        var writer = new StallDumpWriter(
            _root,
            50,
            _time,
            NullLogger.Instance,
            handle =>
            {
                var now = Interlocked.Increment(ref active);
                InterlockedMax(ref mostAtOnce, now);
                Thread.Sleep(15);

                // Inside the lock, so the next dump gets a name of its own.
                _time.Advance(TimeSpan.FromMilliseconds(5));
                var written = WriteBytes(handle);
                Interlocked.Decrement(ref active);
                return written;
            },
            TimeSpan.Zero);

        // Act
        Parallel.For(0, 8, _ => writer.TryWrite());

        // Assert
        mostAtOnce.Should().Be(1);
        Directory.GetFiles(_root).Should().HaveCount(8);
    }

    /// <summary>
    /// Raises a counter to a value if that is higher than what it holds.
    /// </summary>
    /// <param name="target">The counter.</param>
    /// <param name="value">The value.</param>
    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref target);
        }
        while (value > seen && Interlocked.CompareExchange(ref target, value, seen) != seen);
    }

    /// <summary>
    /// Stands in for the real dump: writes three bytes to the file.
    /// </summary>
    /// <param name="handle">The file to write to.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool WriteBytes(SafeFileHandle handle)
    {
        RandomAccess.Write(handle, new byte[] { 1, 2, 3 }, 0);
        return true;
    }
}
