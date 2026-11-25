using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests.Windows;

/// <summary>
/// Tests for the timing of <see cref="EffectStage"/>.
/// </summary>
public sealed class EffectStageTests
{
    /// <summary>
    /// With motion allowed, a step lasts as long as the effect asked for, slowed by the pace.
    /// </summary>
    [Fact]
    public void Duration_AnimationsEnabled_IsSlowedByPace()
    {
        // Act
        var duration = EffectStage.Duration(100, animationsEnabled: true);

        // Assert
        duration.Should().Be(TimeSpan.FromMilliseconds(100 * EffectStage.Pace));
        EffectStage.Pace.Should().BeGreaterThan(1);
    }

    /// <summary>
    /// With motion not allowed, a step takes no time at all, whatever the pace.
    /// </summary>
    [Fact]
    public void Duration_AnimationsDisabled_IsZero()
    {
        // Act
        var duration = EffectStage.Duration(100, animationsEnabled: false);

        // Assert
        duration.Should().Be(TimeSpan.Zero);
    }
}
