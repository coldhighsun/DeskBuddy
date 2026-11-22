using DeskBuddy.App.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="SlowTickLog"/>. The throttling itself is done by the ThrottledLogging package on the real clock,
/// so what is checked here is what is handed to it: which ticks count as slow and which share a throttle.
/// </summary>
public sealed class SlowTickLogTests
{
    /// <summary>
    /// A gap so long that nothing logged is ever logged a second time within a test.
    /// </summary>
    private static readonly TimeSpan LongGap = TimeSpan.FromHours(1);

    /// <summary>
    /// Collects what is logged.
    /// </summary>
    private readonly CollectingLogger _logger = new();

    /// <summary>
    /// A tick that is quick enough is not logged.
    /// </summary>
    [Fact]
    public void Report_QuickTick_LogsNothing()
    {
        // Arrange
        var log = new SlowTickLog(_logger, LongGap);

        // Act
        log.Report("move", SlowTickLog.Threshold - TimeSpan.FromMilliseconds(1));

        // Assert
        _logger.Count.Should().Be(0);
    }

    /// <summary>
    /// A slow tick is logged.
    /// </summary>
    [Fact]
    public void Report_SlowTick_LogsIt()
    {
        // Arrange
        var log = new SlowTickLog(_logger, LongGap);

        // Act
        log.Report("move", SlowTickLog.Threshold);

        // Assert
        _logger.Count.Should().Be(1);
    }

    /// <summary>
    /// A tick that is slow again and again is logged once in a while, per kind of tick.
    /// </summary>
    [Fact]
    public void Report_RepeatedSlowTicks_AreThrottledPerTick()
    {
        // Arrange
        var log = new SlowTickLog(_logger, LongGap);
        var slow = SlowTickLog.Threshold * 2;
        log.Report("move", slow);

        // Act
        log.Report("move", slow);
        log.Report("brain", slow);

        // Assert
        _logger.Count.Should().Be(2);
    }

    /// <summary>
    /// Once the gap is over, the tick is logged again.
    /// </summary>
    [Fact]
    public void Report_AfterTheGap_LogsAgain()
    {
        // Arrange
        var log = new SlowTickLog(_logger, TimeSpan.FromMilliseconds(30));
        var slow = SlowTickLog.Threshold * 2;
        log.Report("move", slow);

        // Act
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_logger.Count < 2 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
            log.Report("move", slow);
        }

        // Assert
        _logger.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// A logger that only counts the entries.
    /// </summary>
    private sealed class CollectingLogger : ILogger
    {
        /// <summary>
        /// The count of entries, changed from any thread.
        /// </summary>
        private int _count;

        /// <summary>
        /// Gets how many entries were logged.
        /// </summary>
        public int Count => Volatile.Read(ref _count);

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Interlocked.Increment(ref _count);
        }
    }
}
