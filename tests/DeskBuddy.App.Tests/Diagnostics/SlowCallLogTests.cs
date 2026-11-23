using DeskBuddy.App.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="SlowCallLog"/>. The throttling is done by the ThrottledLogging package on the real clock, so what
/// is checked here is what is handed to it: which calls count as slow, how they are worded, and which share a throttle.
/// </summary>
public sealed class SlowCallLogTests
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
    /// A call that is quick enough is not logged.
    /// </summary>
    [Fact]
    public void Report_QuickCall_LogsNothing()
    {
        // Arrange
        var log = new SlowCallLog(_logger, LongGap);

        // Act
        log.Report("SetWindowPos", SlowCallLog.Threshold - TimeSpan.FromMilliseconds(1));

        // Assert
        _logger.Messages.Should().BeEmpty();
    }

    /// <summary>
    /// A slow call is logged as a call, by name, and not as a tick of a timer.
    /// </summary>
    [Fact]
    public void Report_SlowCall_NamesTheCallAndSaysHowLongItTook()
    {
        // Arrange
        var log = new SlowCallLog(_logger, LongGap);

        // Act
        log.Report("SetWindowPos", TimeSpan.FromMilliseconds(312));

        // Assert
        var message = _logger.Messages.Should().ContainSingle().Subject;
        message.Should().Contain("SetWindowPos").And.Contain("312");
        message.Should().NotContain("tick");
    }

    /// <summary>
    /// A call that is slow again and again is logged once in a while, per call.
    /// </summary>
    [Fact]
    public void Report_RepeatedSlowCalls_AreThrottledPerCall()
    {
        // Arrange
        var log = new SlowCallLog(_logger, LongGap);
        var slow = SlowCallLog.Threshold * 2;
        log.Report("SetWindowPos", slow);

        // Act
        log.Report("SetWindowPos", slow);
        log.Report("GetWindowRect", slow);

        // Assert
        _logger.Messages.Should().HaveCount(2);
    }

    /// <summary>
    /// A logger that keeps the text of what is logged.
    /// </summary>
    private sealed class CollectingLogger : ILogger
    {
        /// <summary>
        /// The entries logged, changed from any thread.
        /// </summary>
        private readonly List<string> _messages = [];

        /// <summary>
        /// Gets what was logged, as text.
        /// </summary>
        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
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
            lock (_messages)
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }
}
