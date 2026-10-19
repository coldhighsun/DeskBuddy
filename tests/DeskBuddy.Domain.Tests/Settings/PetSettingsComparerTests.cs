using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Domain.Tests.Settings;

/// <summary>
/// Tests for <see cref="PetSettingsComparer"/>.
/// </summary>
public class PetSettingsComparerTests
{
    /// <summary>
    /// Settings with lists holding the same names are equal even though the lists are different objects.
    /// </summary>
    [Fact]
    public void Equals_SameValuesInDifferentLists_AreEqual()
    {
        // Arrange
        var left = PetSettings.Default with { DevProcesses = ["code", "rider"] };
        var right = PetSettings.Default with { DevProcesses = ["code", "rider"] };

        // Act
        var equal = PetSettingsComparer.Instance.Equals(left, right);

        // Assert
        equal.Should().BeTrue();
        PetSettingsComparer.Instance.GetHashCode(left).Should().Be(PetSettingsComparer.Instance.GetHashCode(right));
    }

    /// <summary>
    /// A different item in a list makes the settings different.
    /// </summary>
    [Fact]
    public void Equals_ListsDiffer_AreNotEqual()
    {
        // Arrange
        var left = PetSettings.Default with { ChatProcesses = ["wechat"] };
        var right = PetSettings.Default with { ChatProcesses = ["slack"] };

        // Act
        var equal = PetSettingsComparer.Instance.Equals(left, right);

        // Assert
        equal.Should().BeFalse();
    }

    /// <summary>
    /// A list in a different order counts as a change, as the settings keep the order the user typed.
    /// </summary>
    [Fact]
    public void Equals_SameItemsInOtherOrder_AreNotEqual()
    {
        // Arrange
        var left = PetSettings.Default with { MeetingProcesses = ["a", "b"] };
        var right = PetSettings.Default with { MeetingProcesses = ["b", "a"] };

        // Act
        var equal = PetSettingsComparer.Instance.Equals(left, right);

        // Assert
        equal.Should().BeFalse();
    }

    /// <summary>
    /// A plain value such as the size makes the settings different.
    /// </summary>
    [Fact]
    public void Equals_ScalarDiffers_AreNotEqual()
    {
        // Arrange
        var left = PetSettings.Default with { Size = PetSize.Small };
        var right = PetSettings.Default with { Size = PetSize.Large };

        // Act
        var equal = PetSettingsComparer.Instance.Equals(left, right);

        // Assert
        equal.Should().BeFalse();
    }

    /// <summary>
    /// Every property is part of the comparison, so a setting added later cannot be forgotten.
    /// </summary>
    [Fact]
    public void Equals_EachPropertyChanged_IsNoticed()
    {
        // Arrange
        var baseline = PetSettings.Default;
        var changes = new Dictionary<string, PetSettings>
        {
            [nameof(PetSettings.PetName)] = baseline with { PetName = "other" },
            [nameof(PetSettings.SleepAfter)] = baseline with { SleepAfter = baseline.SleepAfter + TimeSpan.FromMinutes(1) },
            [nameof(PetSettings.SedentaryReminderAfter)] = baseline with { SedentaryReminderAfter = baseline.SedentaryReminderAfter + TimeSpan.FromMinutes(1) },
            [nameof(PetSettings.BossKey)] = baseline with { BossKey = new HotkeyGesture(HotkeyModifiers.Control, 'J') },
            [nameof(PetSettings.AutoHideWhenFullscreen)] = baseline with { AutoHideWhenFullscreen = !baseline.AutoHideWhenFullscreen },
            [nameof(PetSettings.DetectCoveredMonitor)] = baseline with { DetectCoveredMonitor = !baseline.DetectCoveredMonitor },
            [nameof(PetSettings.BlacklistedProcesses)] = baseline with { BlacklistedProcesses = ["x"] },
            [nameof(PetSettings.DevProcesses)] = baseline with { DevProcesses = ["x"] },
            [nameof(PetSettings.ScenariosEnabled)] = baseline with { ScenariosEnabled = !baseline.ScenariosEnabled },
            [nameof(PetSettings.DisabledScenarios)] = baseline with { DisabledScenarios = [] },
            [nameof(PetSettings.Language)] = baseline with { Language = AppLanguage.En },
            [nameof(PetSettings.Theme)] = baseline with { Theme = AppTheme.Dark },
            [nameof(PetSettings.Size)] = baseline with { Size = PetSize.Large },
            [nameof(PetSettings.ClimbEdges)] = baseline with { ClimbEdges = !baseline.ClimbEdges },
            [nameof(PetSettings.ChatterEnabled)] = baseline with { ChatterEnabled = !baseline.ChatterEnabled },
            [nameof(PetSettings.StrollWhileBusy)] = baseline with { StrollWhileBusy = !baseline.StrollWhileBusy },
            [nameof(PetSettings.ClockOutTime)] = baseline with { ClockOutTime = baseline.ClockOutTime + TimeSpan.FromMinutes(10) },
            [nameof(PetSettings.MeetingProcesses)] = baseline with { MeetingProcesses = ["x"] },
            [nameof(PetSettings.ChatProcesses)] = baseline with { ChatProcesses = ["x"] },
        };
        var properties = typeof(PetSettings).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(property => property.Name);

        // Act
        var unnoticed = changes
            .Where(change => PetSettingsComparer.Instance.Equals(baseline, change.Value))
            .Select(change => change.Key);

        // Assert
        changes.Keys.Should().BeEquivalentTo(properties, "every setting needs a case here");
        unnoticed.Should().BeEmpty();
    }
}
