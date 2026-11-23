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
            ChatterEnabled = false,
            PetName = "cat",
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.SleepAfter.Should().Be(TimeSpan.FromMinutes(50));
        normalized.ChatterEnabled.Should().BeFalse();
        normalized.PetName.Should().Be("cat");
    }

    /// <summary>
    /// A blank pet name falls back to the default pet, and spaces around a name are removed.
    /// </summary>
    [Theory]
    [InlineData("", PetSettings.DefaultPetName)]
    [InlineData("   ", PetSettings.DefaultPetName)]
    [InlineData("  cat ", "cat")]
    [InlineData("..", PetSettings.DefaultPetName)]
    [InlineData(".", PetSettings.DefaultPetName)]
    [InlineData(@"..\x", PetSettings.DefaultPetName)]
    [InlineData("a/b", PetSettings.DefaultPetName)]
    [InlineData(@"C:\pets\cat", PetSettings.DefaultPetName)]
    [InlineData("ca*t", PetSettings.DefaultPetName)]
    public void Normalize_PetName_IsTrimmedOrDefaulted(string name, string expected)
    {
        // Act
        var normalized = (PetSettings.Default with { PetName = name }).Normalize();

        // Assert
        normalized.PetName.Should().Be(expected);
    }

    /// <summary>
    /// A removed boss key stays removed.
    /// </summary>
    [Fact]
    public void Normalize_NoBossKey_StaysNone()
    {
        // Act
        var normalized = (PetSettings.Default with { BossKey = null }).Normalize();

        // Assert
        normalized.BossKey.Should().BeNull();
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
            BlacklistedProcesses = ["  Game.exe ", "game", "", "   ", "Teams.EXE", ".exe", "launcher"],
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.BlacklistedProcesses.Should().Equal("Game", "Teams", "launcher");
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
    /// Common editors and terminals are recognized as development tools by default.
    /// </summary>
    [Fact]
    public void Default_DevProcesses_ContainCommonTools()
    {
        // Assert
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
    /// Common chat applications are recognized by default.
    /// </summary>
    [Fact]
    public void Default_ChatProcesses_ContainCommonApplications()
    {
        // Assert
        PetSettings.Default.ChatProcesses.Should().Contain(["Weixin", "WhatsApp"]);
    }

    /// <summary>
    /// The list of chat applications is cleaned up like the other process lists.
    /// </summary>
    [Fact]
    public void Normalize_ChatProcesses_AreCleanedUp()
    {
        // Arrange
        var settings = PetSettings.Default with { ChatProcesses = [" Weixin.exe ", "weixin", "", "Line.EXE"] };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.ChatProcesses.Should().Equal("Weixin", "Line");
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
    /// The animation for switching pets is a random one until the user picks one.
    /// </summary>
    [Fact]
    public void Default_SwitchEffect_IsRandom()
    {
        // Assert
        PetSettings.Default.SwitchEffect.Should().Be(PetSwitchEffect.Random);
    }

    /// <summary>
    /// A switch animation that does not exist falls back to a random one.
    /// </summary>
    [Fact]
    public void Normalize_UnknownSwitchEffect_FallsBackToRandom()
    {
        // Arrange
        var settings = PetSettings.Default with { SwitchEffect = (PetSwitchEffect)42 };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.SwitchEffect.Should().Be(PetSwitchEffect.Random);
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
    /// Strolling out of states such as coding or a meeting is on until the user switches it off.
    /// </summary>
    [Fact]
    public void Default_StrollWhileBusy_IsOn()
    {
        // Assert
        PetSettings.Default.StrollWhileBusy.Should().BeTrue();
    }

    /// <summary>
    /// Reacting to clicks is on until the user switches it off.
    /// </summary>
    [Fact]
    public void Default_InteractionReactions_IsOn()
    {
        // Assert
        PetSettings.Default.InteractionReactions.Should().BeTrue();
    }

    /// <summary>
    /// Looking for updates by itself is on until the user switches it off.
    /// </summary>
    [Fact]
    public void Default_AutoCheckUpdates_IsOn()
    {
        // Assert
        PetSettings.Default.AutoCheckUpdates.Should().BeTrue();
    }

    /// <summary>
    /// Changing the pet by itself is off, and every 30 minutes once it is switched on, until the user chooses otherwise.
    /// </summary>
    [Fact]
    public void Default_AutoSwitchPet_IsOffEvery30Minutes()
    {
        // Assert
        PetSettings.Default.AutoSwitchPet.Should().BeFalse();
        PetSettings.Default.AutoSwitchPetInterval.Should().Be(TimeSpan.FromMinutes(30));
    }

    /// <summary>
    /// Each interval the user can choose is kept as it is.
    /// </summary>
    [Fact]
    public void Normalize_AutoSwitchIntervalOnTheList_IsKept()
    {
        foreach (var interval in PetSettings.AutoSwitchIntervals)
        {
            // Act
            var normalized = (PetSettings.Default with { AutoSwitchPetInterval = interval }).Normalize();

            // Assert
            normalized.AutoSwitchPetInterval.Should().Be(interval);
        }
    }

    /// <summary>
    /// An interval that is not on the list (a hand-edited file) goes back to the default.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(45)]
    [InlineData(100000)]
    public void Normalize_AutoSwitchIntervalOffTheList_FallsBackToDefault(int minutes)
    {
        // Act
        var normalized = (PetSettings.Default with { AutoSwitchPetInterval = TimeSpan.FromMinutes(minutes) }).Normalize();

        // Assert
        normalized.AutoSwitchPetInterval.Should().Be(PetSettings.Default.AutoSwitchPetInterval);
    }

    /// <summary>
    /// A foreground window covering a whole monitor counts as full screen unless the user switches that off.
    /// </summary>
    [Fact]
    public void Default_DetectCoveredMonitor_IsOn()
    {
        // Assert
        PetSettings.Default.DetectCoveredMonitor.Should().BeTrue();
    }

    /// <summary>
    /// Casual remarks are on until the user switches them off.
    /// </summary>
    [Fact]
    public void Default_ChatterEnabled_IsOn()
    {
        // Assert
        PetSettings.Default.ChatterEnabled.Should().BeTrue();
    }

    /// <summary>
    /// Normalizing keeps the choice of casual remarks as it is.
    /// </summary>
    [Fact]
    public void Normalize_ChatterEnabled_IsKept()
    {
        // Act
        var normalized = (PetSettings.Default with { ChatterEnabled = false }).Normalize();

        // Assert
        normalized.ChatterEnabled.Should().BeFalse();
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
    /// The three sizes show a 64 pixel frame at 48, 64 and 96 pixels.
    /// </summary>
    [Theory]
    [InlineData(PetSize.Small, 48)]
    [InlineData(PetSize.Medium, 64)]
    [InlineData(PetSize.Large, 96)]
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
        PetSize.Medium.Scale(96).Should().Be(96);
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

    /// <summary>
    /// No pet is switched off by default.
    /// </summary>
    [Fact]
    public void Default_HiddenPets_IsEmpty()
    {
        // Assert
        PetSettings.Default.HiddenPets.Should().BeEmpty();
    }

    /// <summary>
    /// The list of pets that are switched off is trimmed, de-duplicated ignoring case, and loses anything that is not a
    /// plain folder name, as no pet can have such a name.
    /// </summary>
    [Fact]
    public void Normalize_HiddenPets_AreTrimmedDistinctAndPlainFolderNames()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            HiddenPets = [" cat ", "CAT", "dog", "", "  ", ".", "..", "a/b", "a\b", "c:d", "bad|name", "fox"],
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.HiddenPets.Should().Equal("cat", "dog", "fox");
    }

    /// <summary>
    /// A list that is missing (a <see langword="null"/> from a hand-edited file) becomes an empty one.
    /// </summary>
    [Fact]
    public void Normalize_HiddenPetsNull_BecomesEmpty()
    {
        // Arrange
        var settings = PetSettings.Default with { HiddenPets = null! };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.HiddenPets.Should().BeEmpty();
    }

    /// <summary>
    /// A plain folder name is accepted; an empty one, a path and names a file cannot have are not.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="expected">Whether it counts as a plain folder name.</param>
    [Theory]
    [InlineData("cat", true)]
    [InlineData("my pet 2", true)]
    [InlineData("猫咪", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a/b", false)]
    [InlineData("a\b", false)]
    [InlineData("c:d", false)]
    [InlineData("what?", false)]
    public void IsPlainFolderName_Name_TellsWhetherItCanNameAFolder(string? name, bool expected)
    {
        // Act
        var result = PetSettings.IsPlainFolderName(name);

        // Assert
        result.Should().Be(expected);
    }
}
