using AwesomeAssertions;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Domain.Tests.Settings;

/// <summary>
/// Tests for <see cref="PetSettings"/>.
/// </summary>
public class PetSettingsTests
{
    /// <summary>
    /// The defaults are already inside the allowed ranges, so normalizing changes nothing.
    /// </summary>
    [Fact]
    public void Normalize_Defaults_AreUnchanged()
    {
        // Act
        var normalized = PetSettings.Default.Normalize();

        // Assert
        normalized.Should().BeEquivalentTo(PetSettings.Default);
    }

    /// <summary>
    /// Durations below their minimum are raised to it.
    /// </summary>
    [Fact]
    public void Normalize_DurationsTooShort_AreRaisedToMinimum()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            SleepAfter = TimeSpan.Zero,
            PomodoroWork = TimeSpan.FromSeconds(1),
            PomodoroShortBreak = TimeSpan.FromSeconds(-5),
            PomodoroLongBreak = TimeSpan.Zero,
            SedentaryReminderAfter = TimeSpan.FromSeconds(10),
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.SleepAfter.Should().Be(PetSettings.MinSleepAfter);
        normalized.PomodoroWork.Should().Be(PetSettings.MinPhaseDuration);
        normalized.PomodoroShortBreak.Should().Be(PetSettings.MinPhaseDuration);
        normalized.PomodoroLongBreak.Should().Be(PetSettings.MinPhaseDuration);
        normalized.SedentaryReminderAfter.Should().Be(PetSettings.MinSedentaryInterval);
    }

    /// <summary>
    /// Durations above their maximum are lowered to it.
    /// </summary>
    [Fact]
    public void Normalize_DurationsTooLong_AreLoweredToMaximum()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            SleepAfter = TimeSpan.FromDays(3),
            PomodoroWork = TimeSpan.FromHours(10),
            PomodoroShortBreak = TimeSpan.FromHours(10),
            PomodoroLongBreak = TimeSpan.FromHours(10),
            SedentaryReminderAfter = TimeSpan.FromDays(1),
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.SleepAfter.Should().Be(PetSettings.MaxSleepAfter);
        normalized.PomodoroWork.Should().Be(PetSettings.MaxWorkDuration);
        normalized.PomodoroShortBreak.Should().Be(PetSettings.MaxBreakDuration);
        normalized.PomodoroLongBreak.Should().Be(PetSettings.MaxBreakDuration);
        normalized.SedentaryReminderAfter.Should().Be(PetSettings.MaxSedentaryInterval);
    }

    /// <summary>
    /// Values inside the range are kept as they are.
    /// </summary>
    [Fact]
    public void Normalize_ValuesInRange_AreKept()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            PomodoroWork = TimeSpan.FromMinutes(50),
            Mode = DisturbanceMode.Roam,
            SkinName = "cat",
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.PomodoroWork.Should().Be(TimeSpan.FromMinutes(50));
        normalized.Mode.Should().Be(DisturbanceMode.Roam);
        normalized.SkinName.Should().Be("cat");
    }

    /// <summary>
    /// A mode that is not defined falls back to the default.
    /// </summary>
    [Fact]
    public void Normalize_UnknownMode_FallsBackToDefault()
    {
        // Act
        var normalized = (PetSettings.Default with { Mode = (DisturbanceMode)42 }).Normalize();

        // Assert
        normalized.Mode.Should().Be(PetSettings.Default.Mode);
    }

    /// <summary>
    /// A blank skin name falls back to the default skin, and spaces around a name are removed.
    /// </summary>
    [Theory]
    [InlineData("", PetSettings.DefaultSkinName)]
    [InlineData("   ", PetSettings.DefaultSkinName)]
    [InlineData("  cat ", "cat")]
    public void Normalize_SkinName_IsTrimmedOrDefaulted(string name, string expected)
    {
        // Act
        var normalized = (PetSettings.Default with { SkinName = name }).Normalize();

        // Assert
        normalized.SkinName.Should().Be(expected);
    }

    /// <summary>
    /// An invalid boss key is replaced by the default one.
    /// </summary>
    [Fact]
    public void Normalize_InvalidBossKey_FallsBackToDefault()
    {
        // Act
        var normalized = (PetSettings.Default with { BossKey = new HotkeyGesture(HotkeyModifiers.None, 'H') }).Normalize();

        // Assert
        normalized.BossKey.Should().Be(PetSettings.Default.BossKey);
    }

    /// <summary>
    /// Process names are trimmed, stripped of <c>.exe</c>, de-duplicated ignoring case, and blanks are dropped.
    /// </summary>
    [Fact]
    public void Normalize_ProcessNames_AreCleanedUp()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            BlacklistedProcesses = ["  Zoom.exe ", "zoom", "", "   ", "Teams.EXE", ".exe", "wemeetapp"],
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.BlacklistedProcesses.Should().Equal("Zoom", "Teams", "wemeetapp");
    }

    /// <summary>
    /// A missing process list becomes an empty one.
    /// </summary>
    [Fact]
    public void Normalize_NullProcessList_BecomesEmpty()
    {
        // Act
        var normalized = (PetSettings.Default with { BlacklistedProcesses = null! }).Normalize();

        // Assert
        normalized.BlacklistedProcesses.Should().BeEmpty();
    }
}
