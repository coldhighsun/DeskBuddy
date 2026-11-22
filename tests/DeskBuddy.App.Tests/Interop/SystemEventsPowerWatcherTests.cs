using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Power;
using DeskBuddy.App.Interop;
using Microsoft.Win32;

namespace DeskBuddy.App.Tests.Interop;

/// <summary>
/// Tests for <see cref="SystemEventsPowerWatcher"/>.
/// </summary>
public class SystemEventsPowerWatcherTests
{
    /// <summary>
    /// A change of the power status and waking up make the status be read again; going to sleep does not.
    /// </summary>
    [Theory]
    [InlineData(PowerModes.StatusChange, true)]
    [InlineData(PowerModes.Resume, true)]
    [InlineData(PowerModes.Suspend, false)]
    public void ChangesStatus_DependsOnTheKindOfEvent(PowerModes mode, bool expected)
    {
        // Act
        var changes = SystemEventsPowerWatcher.ChangesStatus(mode);

        // Assert
        changes.Should().Be(expected);
    }

    /// <summary>
    /// The status as it is now is published at once, with nothing to wait for, and the watcher can be stopped twice.
    /// </summary>
    [Fact]
    public void Constructor_PublishesTheStatusAtOnce()
    {
        // Arrange
        var published = new List<PowerStatus>();
        var expected = new PowerStatus(true, 50, false);

        // Act
        var watcher = new SystemEventsPowerWatcher(new FixedPower(expected), published.Add, NullLogger<SystemEventsPowerWatcher>.Instance);
        watcher.Dispose();
        var again = () => watcher.Dispose();

        // Assert
        published.Should().Equal(expected);
        again.Should().NotThrow();
    }

    /// <summary>
    /// A reading that fails at the start is not thrown on, as the same code runs on the system's own thread later.
    /// </summary>
    [Fact]
    public void Constructor_WhenReadingFails_DoesNotThrow()
    {
        // Act
        var create = () =>
        {
            using var watcher = new SystemEventsPowerWatcher(new FailingPower(), _ => { }, NullLogger<SystemEventsPowerWatcher>.Instance);
        };

        // Assert
        create.Should().NotThrow();
    }

    /// <summary>
    /// A power supply that always reports the same status.
    /// </summary>
    /// <param name="status">The status to report.</param>
    private sealed class FixedPower(PowerStatus status) : IPowerStatusProvider
    {
        /// <inheritdoc />
        public PowerStatus GetStatus()
        {
            return status;
        }
    }

    /// <summary>
    /// A power supply that cannot be read.
    /// </summary>
    private sealed class FailingPower : IPowerStatusProvider
    {
        /// <inheritdoc />
        public PowerStatus GetStatus()
        {
            throw new InvalidOperationException("no battery service");
        }
    }
}
