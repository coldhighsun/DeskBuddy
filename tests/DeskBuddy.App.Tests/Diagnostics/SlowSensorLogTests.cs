using DeskBuddy.App.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="SlowSensorLog"/>. The throttling itself is done by the ThrottledLogging package on the real
/// clock, so what is checked here is what is handed to it: which readings count as slow and which share a throttle.
/// </summary>
public sealed class SlowSensorLogTests
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
    /// A reading that is quick enough is not logged.
    /// </summary>
    [Fact]
    public void Report_QuickReading_LogsNothing()
    {
        // Arrange
        var log = new SlowSensorLog(_logger, LongGap);

        // Act
        log.Report("process list", SlowSensorLog.Threshold - TimeSpan.FromMilliseconds(1));

        // Assert
        _logger.Messages.Should().BeEmpty();
    }

    /// <summary>
    /// A slow reading is logged with the name of the sensor and how long it took.
    /// </summary>
    [Fact]
    public void Report_SlowReading_LogsNameAndDuration()
    {
        // Arrange
        var log = new SlowSensorLog(_logger, LongGap);

        // Act
        log.Report("process list", TimeSpan.FromMilliseconds(250));

        // Assert
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("process list").And.Contain("250");
    }

    /// <summary>
    /// A sensor that is slow again and again is logged once in a while, per sensor.
    /// </summary>
    [Fact]
    public void Report_RepeatedSlowReadings_AreThrottledPerSensor()
    {
        // Arrange
        var log = new SlowSensorLog(_logger, LongGap);
        var slow = SlowSensorLog.Threshold * 2;
        log.Report("process list", slow);

        // Act
        log.Report("process list", slow);
        log.Report("full-screen check", slow);

        // Assert
        _logger.Messages.Should().HaveCount(2);
    }

    /// <summary>
    /// Once the gap is over, the sensor is logged again.
    /// </summary>
    [Fact]
    public void Report_AfterTheGap_LogsAgain()
    {
        // Arrange
        var log = new SlowSensorLog(_logger, TimeSpan.FromMilliseconds(30));
        var slow = SlowSensorLog.Threshold * 2;
        log.Report("process list", slow);

        // Act
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_logger.Messages.Count < 2 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
            log.Report("process list", slow);
        }

        // Assert
        _logger.Messages.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// A sensor and a tick of the same name do not share a throttle, as they are different kinds of slow event.
    /// </summary>
    [Fact]
    public void Report_SameNameAsATick_DoesNotShareItsThrottle()
    {
        // Arrange
        var slow = SlowSensorLog.Threshold * 2;
        var tickLog = new SlowTickLog(_logger, LongGap);
        var sensorLog = new SlowSensorLog(_logger, LongGap);
        tickLog.Report("brain", slow);

        // Act
        sensorLog.Report("brain", slow);

        // Assert
        _logger.Messages.Should().HaveCount(2);
    }

    /// <summary>
    /// The diagnostics given to a background sensor log slow readings and failures; a failure is always logged.
    /// </summary>
    [Fact]
    public void Diagnostics_ReportsSlowReadingsAndFailures()
    {
        // Arrange
        var diagnostics = new SlowSensorLog(_logger, LongGap).Diagnostics;

        // Act
        diagnostics.Timed("foreground program", TimeSpan.FromSeconds(1));
        diagnostics.Failed("process list", new InvalidOperationException("boom"));

        // Assert
        _logger.Messages.Should().HaveCount(2);
        _logger.Messages[1].Should().Contain("process list");
    }

    /// <summary>
    /// A logger that keeps the formatted entries, from any thread.
    /// </summary>
    private sealed class CollectingLogger : ILogger
    {
        /// <summary>
        /// Guards <see cref="Messages"/>.
        /// </summary>
        private readonly object _gate = new();

        /// <summary>
        /// The entries logged so far.
        /// </summary>
        private readonly List<string> _messages = [];

        /// <summary>
        /// Gets a copy of the entries logged so far.
        /// </summary>
        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_gate)
                {
                    return [.. _messages];
                }
            }
        }

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
            lock (_gate)
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }
}
