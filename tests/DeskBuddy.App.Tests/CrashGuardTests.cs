using AwesomeAssertions;
using DeskBuddy.App.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="CrashGuard"/>.
/// </summary>
public sealed class CrashGuardTests
{
    /// <summary>
    /// The clock the guard reads.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// How many times the user was told.
    /// </summary>
    private int _notices;

    /// <summary>
    /// How many times the logs were flushed.
    /// </summary>
    private int _flushes;

    /// <summary>
    /// Creates a guard wired to the counters.
    /// </summary>
    /// <returns>
    /// The guard.
    /// </returns>
    private CrashGuard CreateGuard()
    {
        return new CrashGuard(NullLogger.Instance, _time, () => _notices++, () => _flushes++);
    }

    /// <summary>
    /// A single error is survived and the user is told.
    /// </summary>
    [Fact]
    public void HandleUiException_FirstError_CarriesOnAndNotifies()
    {
        // Arrange
        var guard = CreateGuard();

        // Act
        var carryOn = guard.HandleUiException(new InvalidOperationException());

        // Assert
        carryOn.Should().BeTrue();
        _notices.Should().Be(1);
    }

    /// <summary>
    /// Errors in quick succession tell the user only once.
    /// </summary>
    [Fact]
    public void HandleUiException_ErrorsInQuickSuccession_NotifiesOnce()
    {
        // Arrange
        var guard = CreateGuard();

        // Act
        guard.HandleUiException(new InvalidOperationException());
        _time.Advance(TimeSpan.FromSeconds(8));
        guard.HandleUiException(new InvalidOperationException());

        // Assert
        _notices.Should().Be(1);
    }

    /// <summary>
    /// After the notice interval the user is told again.
    /// </summary>
    [Fact]
    public void HandleUiException_AfterNoticeInterval_NotifiesAgain()
    {
        // Arrange
        var guard = CreateGuard();
        guard.HandleUiException(new InvalidOperationException());

        // Act
        _time.Advance(TimeSpan.FromMinutes(2));
        guard.HandleUiException(new InvalidOperationException());

        // Assert
        _notices.Should().Be(2);
    }

    /// <summary>
    /// A burst of errors means a failing loop: the guard gives up and flushes the log.
    /// </summary>
    [Fact]
    public void HandleUiException_BurstOfErrors_GivesUpAndFlushes()
    {
        // Arrange
        var guard = CreateGuard();

        // Act
        var results = Enumerable.Range(0, 5)
            .Select(_ =>
            {
                _time.Advance(TimeSpan.FromMilliseconds(33));
                return guard.HandleUiException(new InvalidOperationException());
            })
            .ToList();

        // Assert
        results.Should().Equal(true, true, true, true, false);
        _flushes.Should().Be(1);
    }

    /// <summary>
    /// Errors spread over time never reach the burst limit.
    /// </summary>
    [Fact]
    public void HandleUiException_SpreadOverTime_AlwaysCarriesOn()
    {
        // Arrange
        var guard = CreateGuard();

        // Act
        var results = Enumerable.Range(0, 20)
            .Select(_ =>
            {
                _time.Advance(TimeSpan.FromSeconds(5));
                return guard.HandleUiException(new InvalidOperationException());
            })
            .ToList();

        // Assert
        results.Should().OnlyContain(r => r);
    }

    /// <summary>
    /// A fatal error flushes the log, even without an exception object.
    /// </summary>
    [Fact]
    public void HandleFatal_NonExceptionObject_StillFlushes()
    {
        // Arrange
        var guard = CreateGuard();

        // Act
        guard.HandleFatal("not an exception");

        // Assert
        _flushes.Should().Be(1);
    }

    /// <summary>
    /// An unobserved task error is only logged; it neither notifies nor flushes.
    /// </summary>
    [Fact]
    public void HandleUnobservedTask_Error_DoesNotNotify()
    {
        // Arrange
        var guard = CreateGuard();

        // Act
        guard.HandleUnobservedTask(new InvalidOperationException());

        // Assert
        _notices.Should().Be(0);
        _flushes.Should().Be(0);
    }
}
