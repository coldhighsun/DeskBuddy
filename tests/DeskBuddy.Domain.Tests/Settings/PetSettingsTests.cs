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
            SedentaryReminderAfter = TimeSpan.FromSeconds(10),
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.SleepAfter.Should().Be(PetSettings.MinSleepAfter);
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
            SedentaryReminderAfter = TimeSpan.FromDays(1),
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.SleepAfter.Should().Be(PetSettings.MaxSleepAfter);
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
            SleepAfter = TimeSpan.FromMinutes(50),
            ChatterEnabled = true,
            PetName = "cat",
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.SleepAfter.Should().Be(TimeSpan.FromMinutes(50));
        normalized.ChatterEnabled.Should().BeTrue();
        normalized.PetName.Should().Be("cat");
    }

    /// <summary>
    /// A blank pet name falls back to the default pet, and spaces around a name are removed.
    /// </summary>
    [Theory]
    [InlineData("", PetSettings.DefaultPetName)]
    [InlineData("   ", PetSettings.DefaultPetName)]
    [InlineData("  cat ", "cat")]
    public void Normalize_PetName_IsTrimmedOrDefaulted(string name, string expected)
    {
        // Act
        var normalized = (PetSettings.Default with { PetName = name }).Normalize();

        // Assert
        normalized.PetName.Should().Be(expected);
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

    /// <summary>
    /// Development detection is on by default and recognizes common editors and terminals.
    /// </summary>
    [Fact]
    public void Default_DevelopmentDetection_IsOnWithCommonTools()
    {
        // Assert
        PetSettings.Default.DevModeEnabled.Should().BeTrue();
        PetSettings.Default.DevProcesses.Should().Contain(["Code", "devenv", "rider64", "WindowsTerminal"]);
    }

    /// <summary>
    /// Common meeting applications are recognized by default.
    /// </summary>
    [Fact]
    public void Default_MeetingProcesses_ContainCommonApplications()
    {
        // Assert
        PetSettings.Default.MeetingProcesses.Should().Contain(["Zoom", "wemeetapp"]);
    }

    /// <summary>
    /// The list of meeting applications is cleaned up like the other process lists.
    /// </summary>
    [Fact]
    public void Normalize_MeetingProcesses_AreCleanedUp()
    {
        // Arrange
        var settings = PetSettings.Default with { MeetingProcesses = [" Zoom.exe ", "zoom", "", "Teams.EXE"] };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.MeetingProcesses.Should().Equal("Zoom", "Teams");
    }

    /// <summary>
    /// Scenarios are on by default, except the chime on the hour, which would soon become annoying.
    /// </summary>
    [Fact]
    public void Default_Scenarios_AreOnExceptTheChime()
    {
        // Assert
        PetSettings.Default.ScenariosEnabled.Should().BeTrue();
        PetSettings.Default.DisabledScenarios.Should().Equal("Chime");
    }

    /// <summary>
    /// The interface follows the system language unless the user chooses one.
    /// </summary>
    [Fact]
    public void Default_Language_FollowsTheSystem()
    {
        // Assert
        PetSettings.Default.Language.Should().Be(AppLanguage.Auto);
    }

    /// <summary>
    /// A language that does not exist falls back to following the system.
    /// </summary>
    [Fact]
    public void Normalize_UnknownLanguage_FallsBackToAuto()
    {
        // Arrange
        var settings = PetSettings.Default with { Language = (AppLanguage)42 };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.Language.Should().Be(AppLanguage.Auto);
    }

    /// <summary>
    /// The theme follows the system until the user picks one.
    /// </summary>
    [Fact]
    public void Default_Theme_FollowsTheSystem()
    {
        // Assert
        PetSettings.Default.Theme.Should().Be(AppTheme.Auto);
    }

    /// <summary>
    /// A theme that does not exist falls back to following the system.
    /// </summary>
    [Fact]
    public void Normalize_UnknownTheme_FallsBackToAuto()
    {
        // Arrange
        var settings = PetSettings.Default with { Theme = (AppTheme)42 };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.Theme.Should().Be(AppTheme.Auto);
    }

    /// <summary>
    /// Climbing along the edges of the screen is off until the user switches it on.
    /// </summary>
    [Fact]
    public void Default_ClimbEdges_IsOff()
    {
        // Assert
        PetSettings.Default.ClimbEdges.Should().BeFalse();
    }

    /// <summary>
    /// Casual remarks are off until the user switches them on.
    /// </summary>
    [Fact]
    public void Default_ChatterEnabled_IsOff()
    {
        // Assert
        PetSettings.Default.ChatterEnabled.Should().BeFalse();
    }

    /// <summary>
    /// Normalizing keeps the choice of casual remarks as it is.
    /// </summary>
    [Fact]
    public void Normalize_ChatterEnabled_IsKept()
    {
        // Act
        var normalized = (PetSettings.Default with { ChatterEnabled = true }).Normalize();

        // Assert
        normalized.ChatterEnabled.Should().BeTrue();
    }

    /// <summary>
    /// The pet is shown at the medium size unless the user chooses another.
    /// </summary>
    [Fact]
    public void Default_Size_IsMedium()
    {
        // Assert
        PetSettings.Default.Size.Should().Be(PetSize.Medium);
    }

    /// <summary>
    /// A size that does not exist falls back to medium.
    /// </summary>
    [Fact]
    public void Normalize_UnknownSize_FallsBackToMedium()
    {
        // Arrange
        var settings = PetSettings.Default with { Size = (PetSize)42 };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.Size.Should().Be(PetSize.Medium);
    }

    /// <summary>
    /// The three sizes show a 64 pixel frame at 32, 48 and 64 pixels.
    /// </summary>
    [Theory]
    [InlineData(PetSize.Small, 32)]
    [InlineData(PetSize.Medium, 48)]
    [InlineData(PetSize.Large, 64)]
    public void Scale_Frame64_GivesTheSizeInPixels(PetSize size, double expected)
    {
        // Act
        var edge = size.Scale(64);

        // Assert
        edge.Should().Be(expected);
    }

    /// <summary>
    /// A frame that is not square keeps its proportions, and no edge shrinks below one pixel.
    /// </summary>
    [Fact]
    public void Scale_NonSquareAndTinyFrames_KeepProportionsAndStayPositive()
    {
        // Assert
        PetSize.Medium.Scale(96).Should().Be(72);
        PetSize.Small.Scale(1).Should().Be(1);
    }

    /// <summary>
    /// The working day ends at six in the evening unless the user says otherwise.
    /// </summary>
    [Fact]
    public void Default_ClockOutTime_IsSixInTheEvening()
    {
        // Assert
        PetSettings.Default.ClockOutTime.Should().Be(TimeSpan.FromHours(18));
    }

    /// <summary>
    /// A clock-out time that is not a time of day falls back to the default; seconds are dropped.
    /// </summary>
    [Theory]
    [InlineData(-1, 0, 18, 0)]
    [InlineData(24, 0, 18, 0)]
    [InlineData(17, 45, 17, 45)]
    public void Normalize_ClockOutTime_StaysWithinTheDay(int hours, int minutes, int expectedHours, int expectedMinutes)
    {
        // Arrange
        var settings = PetSettings.Default with { ClockOutTime = new TimeSpan(hours, minutes, 30) };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.ClockOutTime.Should().Be(new TimeSpan(expectedHours, expectedMinutes, 0));
    }

    /// <summary>
    /// The list of switched-off scenarios keeps known names in their canonical spelling, without duplicates, and drops
    /// the rest.
    /// </summary>
    [Fact]
    public void Normalize_DisabledScenarios_KeepsOnlyKnownOnes()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            DisabledScenarios = [" buildDone ", "BuildDone", "Nonsense", "", "Idle", "Reading"],
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.DisabledScenarios.Should().Equal("BuildDone", "Reading");
    }

    /// <summary>
    /// A missing list of switched-off scenarios becomes an empty one.
    /// </summary>
    [Fact]
    public void Normalize_NullDisabledScenarios_BecomesEmpty()
    {
        // Act
        var normalized = (PetSettings.Default with { DisabledScenarios = null! }).Normalize();

        // Assert
        normalized.DisabledScenarios.Should().BeEmpty();
    }

    /// <summary>
    /// The list of development tools is cleaned up like the full-screen process list.
    /// </summary>
    [Fact]
    public void Normalize_DevProcesses_AreCleanedUp()
    {
        // Arrange
        var settings = PetSettings.Default with { DevProcesses = ["  Code.exe ", "code", "", "Rider64.EXE"] };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.DevProcesses.Should().Equal("Code", "Rider64");
    }

    /// <summary>
    /// A missing development tool list becomes an empty one.
    /// </summary>
    [Fact]
    public void Normalize_NullDevProcesses_BecomesEmpty()
    {
        // Act
        var normalized = (PetSettings.Default with { DevProcesses = null! }).Normalize();

        // Assert
        normalized.DevProcesses.Should().BeEmpty();
    }
}
