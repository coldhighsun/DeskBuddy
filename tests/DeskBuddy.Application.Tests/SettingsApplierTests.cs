using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Pomodoro;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.Visibility;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="SettingsApplier"/>.
/// </summary>
public class SettingsApplierTests
{
    /// <summary>
    /// The settings service the applier follows.
    /// </summary>
    private readonly SettingsService _settings = new(new NullStore());

    /// <summary>
    /// The behavior options being updated.
    /// </summary>
    private readonly PetBehaviorOptions _behavior = new();

    /// <summary>
    /// The pomodoro options being updated.
    /// </summary>
    private readonly PomodoroOptions _pomodoro = new();

    /// <summary>
    /// The sedentary options being updated.
    /// </summary>
    private readonly SedentaryOptions _sedentary = new();

    /// <summary>
    /// The visibility rules being updated.
    /// </summary>
    private readonly PetVisibility _visibility = new(new FakeTimeProvider(), TimeSpan.FromSeconds(2));

    /// <summary>
    /// Creating the applier copies the current settings immediately.
    /// </summary>
    [Fact]
    public async Task Constructor_CurrentSettings_AreCopiedToOptions()
    {
        // Arrange
        await _settings.UpdateAsync(s => s with { PomodoroWork = TimeSpan.FromMinutes(50), SleepAfter = TimeSpan.FromMinutes(9) });

        // Act
        _ = new SettingsApplier(_settings, _behavior, _pomodoro, _sedentary, _visibility);

        // Assert
        _pomodoro.WorkDuration.Should().Be(TimeSpan.FromMinutes(50));
        _behavior.SleepAfter.Should().Be(TimeSpan.FromMinutes(9));
    }

    /// <summary>
    /// A later change reaches the options without recreating anything.
    /// </summary>
    [Fact]
    public async Task Changed_NewSettings_UpdateAllOptions()
    {
        // Arrange
        _ = new SettingsApplier(_settings, _behavior, _pomodoro, _sedentary, _visibility);

        // Act
        await _settings.UpdateAsync(s => s with
        {
            SleepAfter = TimeSpan.FromMinutes(10),
            PomodoroWork = TimeSpan.FromMinutes(30),
            PomodoroShortBreak = TimeSpan.FromMinutes(6),
            PomodoroLongBreak = TimeSpan.FromMinutes(20),
            SedentaryReminderAfter = TimeSpan.FromMinutes(60),
        });

        // Assert
        _behavior.SleepAfter.Should().Be(TimeSpan.FromMinutes(10));
        _pomodoro.WorkDuration.Should().Be(TimeSpan.FromMinutes(30));
        _pomodoro.ShortBreakDuration.Should().Be(TimeSpan.FromMinutes(6));
        _pomodoro.LongBreakDuration.Should().Be(TimeSpan.FromMinutes(20));
        _sedentary.ReminderAfter.Should().Be(TimeSpan.FromMinutes(60));
    }

    /// <summary>
    /// Turning auto-hide off in the settings reaches the visibility rules.
    /// </summary>
    [Fact]
    public async Task Changed_AutoHideSetting_UpdatesVisibility()
    {
        // Arrange
        _ = new SettingsApplier(_settings, _behavior, _pomodoro, _sedentary, _visibility);

        // Act
        await _settings.UpdateAsync(s => s with { AutoHideWhenFullscreen = false });

        // Assert
        _visibility.AutoHideEnabled.Should().BeFalse();
    }

    /// <summary>
    /// Forced overrides are put back after every application of the settings, so they keep winning.
    /// </summary>
    [Fact]
    public async Task Changed_WithOverrides_OverridesWin()
    {
        // Arrange
        void Override() => _behavior.SleepAfter = TimeSpan.FromSeconds(30);
        _ = new SettingsApplier(_settings, _behavior, _pomodoro, _sedentary, _visibility, Override);

        // Act
        await _settings.UpdateAsync(s => s with { SleepAfter = TimeSpan.FromMinutes(10), PomodoroWork = TimeSpan.FromMinutes(40) });

        // Assert
        _behavior.SleepAfter.Should().Be(TimeSpan.FromSeconds(30));
        _pomodoro.WorkDuration.Should().Be(TimeSpan.FromMinutes(40));
    }

    /// <summary>
    /// Settings storage that keeps nothing.
    /// </summary>
    private sealed class NullStore : ISettingsStore
    {
        /// <inheritdoc />
        public Task<PetSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PetSettings.Default);
        }

        /// <inheritdoc />
        public Task SaveAsync(PetSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
