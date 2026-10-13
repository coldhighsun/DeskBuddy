using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Scenarios;
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
    /// The sedentary options being updated.
    /// </summary>
    private readonly SedentaryOptions _sedentary = new();

    /// <summary>
    /// The visibility rules being updated.
    /// </summary>
    private readonly PetVisibility _visibility = new(new FakeTimeProvider(), TimeSpan.FromSeconds(2));

    /// <summary>
    /// The chatter policy being updated.
    /// </summary>
    private readonly ChatterPolicy _chatter = new(new FakeTimeProvider(), new Random(1));

    /// <summary>
    /// The development detection being updated.
    /// </summary>
    private readonly DevActivityPolicy _devActivity = new(new PetBehaviorOptions(), new FakeTimeProvider());

    /// <summary>
    /// The scenario switches being updated.
    /// </summary>
    private readonly ScenarioSwitches _scenarios = new();

    /// <summary>
    /// The meeting detection being updated.
    /// </summary>
    private readonly MeetingPolicy _meeting = new();

    /// <summary>
    /// Creating the applier copies the current settings immediately.
    /// </summary>
    [Fact]
    public async Task Constructor_CurrentSettings_AreCopiedToOptions()
    {
        // Arrange
        await _settings.UpdateAsync(s => s with { SedentaryReminderAfter = TimeSpan.FromMinutes(50), SleepAfter = TimeSpan.FromMinutes(9) }, TestContext.Current.CancellationToken);

        // Act
        _ = new SettingsApplier(_settings, _behavior, _sedentary, _visibility, _chatter, _devActivity, _scenarios, _meeting);

        // Assert
        _sedentary.ReminderAfter.Should().Be(TimeSpan.FromMinutes(50));
        _behavior.SleepAfter.Should().Be(TimeSpan.FromMinutes(9));
    }

    /// <summary>
    /// A later change reaches the options without recreating anything.
    /// </summary>
    [Fact]
    public async Task Changed_NewSettings_UpdateAllOptions()
    {
        // Arrange
        _ = new SettingsApplier(_settings, _behavior, _sedentary, _visibility, _chatter, _devActivity, _scenarios, _meeting);

        // Act
        await _settings.UpdateAsync(s => s with
        {
            SleepAfter = TimeSpan.FromMinutes(10),
            SedentaryReminderAfter = TimeSpan.FromMinutes(60),
        }, TestContext.Current.CancellationToken);

        // Assert
        _behavior.SleepAfter.Should().Be(TimeSpan.FromMinutes(10));
        _sedentary.ReminderAfter.Should().Be(TimeSpan.FromMinutes(60));
    }

    /// <summary>
    /// Turning auto-hide off in the settings reaches the visibility rules.
    /// </summary>
    [Fact]
    public async Task Changed_AutoHideSetting_UpdatesVisibility()
    {
        // Arrange
        _ = new SettingsApplier(_settings, _behavior, _sedentary, _visibility, _chatter, _devActivity, _scenarios, _meeting);

        // Act
        await _settings.UpdateAsync(s => s with { AutoHideWhenFullscreen = false }, TestContext.Current.CancellationToken);

        // Assert
        _visibility.AutoHideEnabled.Should().BeFalse();
    }

    /// <summary>
    /// Casual remarks are enabled exactly while the pet roams.
    /// </summary>
    [Fact]
    public async Task Changed_Mode_EnablesChatterOnlyWhenRoaming()
    {
        // Arrange
        _ = new SettingsApplier(_settings, _behavior, _sedentary, _visibility, _chatter, _devActivity, _scenarios, _meeting);
        var initially = _chatter.IsEnabled;

        // Act
        await _settings.UpdateAsync(s => s with { Mode = DisturbanceMode.Roam }, TestContext.Current.CancellationToken);
        var roaming = _chatter.IsEnabled;
        await _settings.UpdateAsync(s => s with { Mode = DisturbanceMode.Docked }, TestContext.Current.CancellationToken);

        // Assert
        initially.Should().BeFalse();
        roaming.Should().BeTrue();
        _chatter.IsEnabled.Should().BeFalse();
    }

    /// <summary>
    /// Forced overrides are put back after every application of the settings, so they keep winning.
    /// </summary>
    [Fact]
    public async Task Changed_WithOverrides_OverridesWin()
    {
        // Arrange
        void Override() => _behavior.SleepAfter = TimeSpan.FromSeconds(30);
        _ = new SettingsApplier(_settings, _behavior, _sedentary, _visibility, _chatter, _devActivity, _scenarios, _meeting, Override);

        // Act
        await _settings.UpdateAsync(s => s with { SleepAfter = TimeSpan.FromMinutes(10), SedentaryReminderAfter = TimeSpan.FromMinutes(40) }, TestContext.Current.CancellationToken);

        // Assert
        _behavior.SleepAfter.Should().Be(TimeSpan.FromSeconds(30));
        _sedentary.ReminderAfter.Should().Be(TimeSpan.FromMinutes(40));
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
