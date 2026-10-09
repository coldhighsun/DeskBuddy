using AwesomeAssertions;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Settings;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="MovementSelector"/>.
/// </summary>
public class MovementSelectorTests
{
    /// <summary>
    /// The settings the selector follows.
    /// </summary>
    private readonly SettingsService _settings = new(new NullStore());

    /// <summary>
    /// The docked strategy handed to the selector.
    /// </summary>
    private readonly DockedMovement _docked = new();

    /// <summary>
    /// The roaming strategy handed to the selector.
    /// </summary>
    private readonly RoamMovement _roam = new(new RoamOptions(), new FakeTimeProvider(), new Random(1));

    /// <summary>
    /// The selector under test.
    /// </summary>
    private readonly MovementSelector _selector;

    /// <summary>
    /// Initializes a new instance of the <see cref="MovementSelectorTests"/> class.
    /// </summary>
    public MovementSelectorTests()
    {
        _selector = new MovementSelector(_settings, _docked, _roam);
    }

    /// <summary>
    /// The default mode walks along the taskbar.
    /// </summary>
    [Fact]
    public void Current_DefaultMode_IsDocked()
    {
        // Assert
        _selector.Current.Should().BeSameAs(_docked);
    }

    /// <summary>
    /// Switching the mode in the settings switches the strategy and announces it.
    /// </summary>
    [Fact]
    public async Task Current_ModeChangedToRoam_SwitchesAndRaisesModeChanged()
    {
        // Arrange
        var raised = 0;
        _selector.ModeChanged += (_, _) => raised++;

        // Act
        await _settings.UpdateAsync(s => s with { Mode = DisturbanceMode.Roam });

        // Assert
        _selector.Current.Should().BeSameAs(_roam);
        raised.Should().Be(1);
    }

    /// <summary>
    /// Switching back restores the docked strategy.
    /// </summary>
    [Fact]
    public async Task Current_ModeChangedBackToDocked_SwitchesBack()
    {
        // Arrange
        await _settings.UpdateAsync(s => s with { Mode = DisturbanceMode.Roam });

        // Act
        await _settings.UpdateAsync(s => s with { Mode = DisturbanceMode.Docked });

        // Assert
        _selector.Current.Should().BeSameAs(_docked);
    }

    /// <summary>
    /// A settings change that leaves the mode alone does not announce a mode change.
    /// </summary>
    [Fact]
    public async Task ModeChanged_OtherSettingChanged_IsNotRaised()
    {
        // Arrange
        var raised = 0;
        _selector.ModeChanged += (_, _) => raised++;

        // Act
        await _settings.UpdateAsync(s => s with { SkinName = "other" });

        // Assert
        raised.Should().Be(0);
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
