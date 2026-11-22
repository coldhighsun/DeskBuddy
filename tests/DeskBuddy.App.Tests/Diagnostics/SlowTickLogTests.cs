using DeskBuddy.App.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.App.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="SlowTickLog"/>.
/// </summary>
public sealed class SlowTickLogTests
{
    /// <summary>
    /// The clock the log reads.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

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
        var log = new SlowTickLog(_logger, _time);

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
        var log = new SlowTickLog(_logger, _time);

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
        var log = new SlowTickLog(_logger, _time);
        var slow = SlowTickLog.Threshold * 2;
        log.Report("move", slow);

        // Act
        _time.Advance(TimeSpan.FromMilliseconds(33));
        log.Report("move", slow);
        log.Report("brain", slow);
        _time.Advance(TimeSpan.FromSeconds(2));
        log.Report("move", slow);

        // Assert
        _logger.Count.Should().Be(3);
    }

    /// <summary>
    /// A logger that only counts the entries.
    /// </summary>
    private sealed class CollectingLogger : ILogger
    {
        /// <summary>
        /// Gets how many entries were logged.
        /// </summary>
        public int Count { get; private set; }

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
            Count++;
        }
    }
}
