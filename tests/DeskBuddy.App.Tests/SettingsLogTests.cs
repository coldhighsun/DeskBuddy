using DeskBuddy.App.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="SettingsLog"/>.
/// </summary>
public class SettingsLogTests
{
    /// <summary>
    /// The logger the messages go to.
    /// </summary>
    private readonly CapturingLogger _logger = new();

    /// <summary>
    /// A name taken out for having a space is logged as a warning that says why.
    /// </summary>
    [Fact]
    public void LogRemovedProcessNames_NameWithSpace_LogsWarningWithReason()
    {
        // Arrange
        var removed = new[] { new RemovedProcessName("My Game", ProcessListKind.Fullscreen, ProcessListProblem.ContainsSpace, null) };

        // Act
        SettingsLog.LogRemovedProcessNames(_logger, removed);

        // Assert
        _logger.Entries.Should().ContainSingle();
        _logger.Entries[0].Level.Should().Be(LogLevel.Warning);
        _logger.Entries[0].Message.Should().Contain("My Game").And.Contain("Fullscreen").And.Contain("space");
    }

    /// <summary>
    /// A name taken out because it is in another list is logged with both lists.
    /// </summary>
    [Fact]
    public void LogRemovedProcessNames_NameInAnotherList_LogsBothLists()
    {
        // Arrange
        var removed = new[] { new RemovedProcessName("zoom", ProcessListKind.Fullscreen, ProcessListProblem.InAnotherList, ProcessListKind.Meeting) };

        // Act
        SettingsLog.LogRemovedProcessNames(_logger, removed);

        // Assert
        _logger.Entries.Should().ContainSingle();
        _logger.Entries[0].Message.Should().Contain("zoom").And.Contain("Fullscreen").And.Contain("Meeting");
    }

    /// <summary>
    /// With nothing taken out nothing is logged.
    /// </summary>
    [Fact]
    public void LogRemovedProcessNames_NothingRemoved_LogsNothing()
    {
        // Act
        SettingsLog.LogRemovedProcessNames(_logger, []);

        // Assert
        _logger.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// A logger that keeps what it is given.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        /// <summary>
        /// Gets what was logged, with the level.
        /// </summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

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
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
