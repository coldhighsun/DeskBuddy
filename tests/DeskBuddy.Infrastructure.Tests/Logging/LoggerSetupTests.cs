using DeskBuddy.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DeskBuddy.Infrastructure.Tests.Logging;

/// <summary>
/// Tests for <see cref="LoggerSetup"/>.
/// </summary>
public sealed class LoggerSetupTests : IDisposable
{
    /// <summary>
    /// A fresh directory for each test.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DeskBuddyLogTests-" + Guid.NewGuid().ToString("N"));

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
    /// Entries at or above the minimum level are written, with the exception below the message.
    /// </summary>
    [Fact]
    public void Create_EntryAboveMinimum_IsWrittenWithException()
    {
        // Arrange
        var serilog = LoggerSetup.Create(_directory, LogLevel.Information);
        var logger = new LoggerFactory().AddSerilog(serilog, dispose: true).CreateLogger("Test");

        // Act
        logger.LogInformation("hello {Name}", "pet");
        logger.LogDebug("hidden");
        logger.LogError(new InvalidOperationException("boom"), "failed");
        serilog.Dispose();

        // Assert
        var text = File.ReadAllText(Directory.GetFiles(_directory).Single());
        text.Should().Contain("[INF] Test: hello pet")
            .And.Contain("[ERR] Test: failed")
            .And.Contain("InvalidOperationException: boom")
            .And.NotContain("hidden");
    }

    /// <summary>
    /// A file that reaches the size limit rolls over, and only the newest files are kept.
    /// </summary>
    [Fact]
    public void Create_ManyEntries_RollsBySizeAndKeepsOnlyTheNewestFiles()
    {
        // Arrange
        var serilog = LoggerSetup.Create(_directory, LogLevel.Information, fileSizeLimitBytes: 1024, retainedFileCount: 3);
        var logger = new LoggerFactory().AddSerilog(serilog, dispose: true).CreateLogger("Test");

        // Act
        for (var i = 0; i < 200; i++)
        {
            logger.LogInformation("entry {Index} {Padding}", i, new string('x', 100));
        }

        serilog.Dispose();

        // Assert
        var files = Directory.GetFiles(_directory);
        files.Should().HaveCount(3);
        files.Should().OnlyContain(path => new FileInfo(path).Length <= 2048);
    }

    /// <summary>
    /// The shipped limits are 10 MB per file and 5 files.
    /// </summary>
    [Fact]
    public void Defaults_AreTenMegabytesAndFiveFiles()
    {
        // Assert
        LoggerSetup.DefaultFileSizeLimitBytes.Should().Be(10 * 1024 * 1024);
        LoggerSetup.DefaultRetainedFileCount.Should().Be(5);
    }
}
