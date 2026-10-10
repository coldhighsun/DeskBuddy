using AwesomeAssertions;
using DeskBuddy.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Infrastructure.Tests.Logging;

/// <summary>
/// Tests for <see cref="FileLoggerProvider"/> and <see cref="LogRetention"/>.
/// </summary>
public sealed class FileLoggerProviderTests : IDisposable
{
    /// <summary>
    /// A fresh directory for each test.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DeskBuddyLogTests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// A clock fixed at a known local time.
    /// </summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Deletes the test directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// Entries at or above the minimum level reach the day's file.
    /// </summary>
    [Fact]
    public void Log_EntryAboveMinimum_IsWrittenToTheDaysFile()
    {
        // Arrange
        var path = Path.Combine(_directory, LogRetention.FileNameFor(DateOnly.FromDateTime(_time.GetLocalNow().DateTime)));
        var provider = new FileLoggerProvider(_directory, _time, LogLevel.Information, keepDays: 7);
        var logger = provider.CreateLogger("Test");

        // Act
        logger.LogInformation("hello {Name}", "pet");
        logger.LogDebug("hidden");
        provider.Dispose();

        // Assert
        var text = File.ReadAllText(path);
        text.Should().Contain("[Inf] Test: hello pet");
        text.Should().NotContain("hidden");
    }

    /// <summary>
    /// Flushing waits until earlier entries are on disk, without closing the provider.
    /// </summary>
    [Fact]
    public void Flush_AfterLogging_EntriesAreOnDiskWhileProviderStaysOpen()
    {
        // Arrange
        using var provider = new FileLoggerProvider(_directory, _time, LogLevel.Information, keepDays: 7);
        var logger = provider.CreateLogger("Test");
        logger.LogCritical("last words");

        // Act
        provider.Flush(TimeSpan.FromSeconds(5));

        // Assert
        var text = File.ReadAllText(Directory.GetFiles(_directory).Single());
        text.Should().Contain("[Crt] Test: last words");
    }

    /// <summary>
    /// An exception is written below its message.
    /// </summary>
    [Fact]
    public void Log_WithException_WritesTheExceptionText()
    {
        // Arrange
        var provider = new FileLoggerProvider(_directory, _time, LogLevel.Information, keepDays: 7);
        var logger = provider.CreateLogger("Test");

        // Act
        logger.LogError(new InvalidOperationException("boom"), "failed");
        provider.Dispose();

        // Assert
        var text = File.ReadAllText(Directory.GetFiles(_directory).Single());
        text.Should().Contain("[Err] Test: failed").And.Contain("InvalidOperationException: boom");
    }

    /// <summary>
    /// A new day starts a new file.
    /// </summary>
    [Fact]
    public void Log_AfterMidnight_StartsANewFile()
    {
        // Arrange
        var provider = new FileLoggerProvider(_directory, _time, LogLevel.Information, keepDays: 7);
        var logger = provider.CreateLogger("Test");

        // Act
        logger.LogInformation("day one");
        _time.Advance(TimeSpan.FromDays(1));
        logger.LogInformation("day two");
        provider.Dispose();

        // Assert
        Directory.GetFiles(_directory).Should().HaveCount(2);
    }

    /// <summary>
    /// A directory that cannot be written to does not make logging throw.
    /// </summary>
    [Fact]
    public void Log_DirectoryIsAFile_DoesNotThrow()
    {
        // Arrange
        File.WriteAllText(_directory, "not a directory");
        var provider = new FileLoggerProvider(_directory, _time, LogLevel.Information, keepDays: 7);
        var logger = provider.CreateLogger("Test");

        // Act
        var act = () =>
        {
            logger.LogInformation("lost");
            provider.Dispose();
        };

        // Assert
        act.Should().NotThrow();
        File.Delete(_directory);
    }

    /// <summary>
    /// Files older than the retention window are deleted at start-up, and other files are kept.
    /// </summary>
    [Fact]
    public void Constructor_OldLogs_AreDeletedAndOtherFilesKept()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var old = Path.Combine(_directory, LogRetention.FileNameFor(today.AddDays(-7)));
        var recent = Path.Combine(_directory, LogRetention.FileNameFor(today.AddDays(-6)));
        var other = Path.Combine(_directory, "notes.log");
        foreach (var path in new[] { old, recent, other })
        {
            File.WriteAllText(path, "x");
        }

        // Act
        new FileLoggerProvider(_directory, _time, LogLevel.Information, keepDays: 7).Dispose();

        // Assert
        File.Exists(old).Should().BeFalse();
        File.Exists(recent).Should().BeTrue();
        File.Exists(other).Should().BeTrue();
    }

    /// <summary>
    /// Pruning a directory that does not exist is a no-op.
    /// </summary>
    [Fact]
    public void Prune_MissingDirectory_DoesNothing()
    {
        // Arrange
        var act = () => LogRetention.Prune(_directory, new DateOnly(2026, 10, 10), keepDays: 7);

        // Act and Assert
        act.Should().NotThrow();
    }
}
