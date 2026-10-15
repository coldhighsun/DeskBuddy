using AwesomeAssertions;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Domain.Tests.Settings;

/// <summary>
/// Tests for <see cref="ProcessListRules"/>.
/// </summary>
public class ProcessListRulesTests
{
    /// <summary>
    /// Typed lines are trimmed, stripped of <c>.exe</c> and de-duplicated.
    /// </summary>
    [Fact]
    public void Clean_TypedLines_AreCleanedUp()
    {
        // Act
        var names = ProcessListRules.Clean([" Weixin.exe ", "weixin", "", "Line"]);

        // Assert
        names.Should().Equal("Weixin", "Line");
    }

    /// <summary>
    /// A list of plain names that are in no other list is fine.
    /// </summary>
    [Fact]
    public void Check_PlainNamesInNoOtherList_IsValid()
    {
        // Act
        var check = ProcessListRules.Check(PetSettings.Default, ProcessListKind.Chat, ["Weixin", "Signal"]);

        // Assert
        check.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// A name with a space in it is refused, and named.
    /// </summary>
    [Fact]
    public void Check_NameWithSpace_ContainsSpace()
    {
        // Act
        var check = ProcessListRules.Check(PetSettings.Default, ProcessListKind.Chat, ["Weixin", "My Chat"]);

        // Assert
        check.Problem.Should().Be(ProcessListProblem.ContainsSpace);
        check.Name.Should().Be("My Chat");
    }

    /// <summary>
    /// A name already in another list is refused, ignoring case, and the other list is named.
    /// </summary>
    [Fact]
    public void Check_NameInAnotherList_InAnotherList()
    {
        // Act
        var check = ProcessListRules.Check(PetSettings.Default, ProcessListKind.Chat, ["zoom"]);

        // Assert
        check.Problem.Should().Be(ProcessListProblem.InAnotherList);
        check.Name.Should().Be("zoom");
        check.OtherList.Should().Be(ProcessListKind.Meeting);
    }

    /// <summary>
    /// A name that is in the list being edited itself is no clash.
    /// </summary>
    [Fact]
    public void Check_NameInSameList_IsValid()
    {
        // Act
        var check = ProcessListRules.Check(PetSettings.Default, ProcessListKind.Meeting, ["Zoom"]);

        // Assert
        check.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// A list can be read from the settings and replaced.
    /// </summary>
    [Fact]
    public void With_EachKind_ReplacesOnlyThatList()
    {
        foreach (var kind in ProcessListRules.All)
        {
            // Act
            var changed = ProcessListRules.With(PetSettings.Default, kind, ["Only"]);

            // Assert
            ProcessListRules.Get(changed, kind).Should().Equal("Only");
            ProcessListRules.All.Where(other => other != kind)
                .Should().OnlyContain(other => !ProcessListRules.Get(changed, other).Contains("Only"));
        }
    }

    /// <summary>
    /// The default lists do not share a process.
    /// </summary>
    [Fact]
    public void Default_Lists_ShareNoProcess()
    {
        // Act
        var all = ProcessListRules.All.SelectMany(kind => ProcessListRules.Get(PetSettings.Default, kind)).ToList();

        // Assert
        all.Distinct(StringComparer.OrdinalIgnoreCase).Should().HaveCount(all.Count);
    }

    /// <summary>
    /// A process in several lists stays in the one that matters most: meeting, chat, development, full screen.
    /// </summary>
    [Fact]
    public void Enforce_ProcessInSeveralLists_StaysInTheMostImportantOne()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            MeetingProcesses = ["Zoom", "Weixin"],
            ChatProcesses = ["weixin", "Signal"],
            DevProcesses = ["Code", "signal", "zoom"],
            BlacklistedProcesses = ["ZOOM", "Code", "Game"],
        };

        // Act
        var result = ProcessListRules.Enforce(settings);

        // Assert
        result.Settings.MeetingProcesses.Should().Equal("Zoom", "Weixin");
        result.Settings.ChatProcesses.Should().Equal("Signal");
        result.Settings.DevProcesses.Should().Equal("Code");
        result.Settings.BlacklistedProcesses.Should().Equal("Game");
    }

    /// <summary>
    /// Names with a space are taken out of every list.
    /// </summary>
    [Fact]
    public void Enforce_NamesWithSpace_AreTakenOut()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            MeetingProcesses = ["Zoom", "My Meeting"],
            ChatProcesses = ["Weixin", "Chat App"],
        };

        // Act
        var result = ProcessListRules.Enforce(settings);

        // Assert
        result.Settings.MeetingProcesses.Should().Equal("Zoom");
        result.Settings.ChatProcesses.Should().Equal("Weixin");
    }

    /// <summary>
    /// What is taken out is reported with the list it came from, why, and where the process stays.
    /// </summary>
    [Fact]
    public void Enforce_TakenOutNames_AreReported()
    {
        // Arrange
        var settings = PetSettings.Default with
        {
            MeetingProcesses = ["Zoom", "My Meeting"],
            BlacklistedProcesses = ["zoom", "Game"],
        };

        // Act
        var result = ProcessListRules.Enforce(settings);

        // Assert
        result.Removed.Should().BeEquivalentTo(
        [
            new RemovedProcessName("My Meeting", ProcessListKind.Meeting, ProcessListProblem.ContainsSpace, null),
            new RemovedProcessName("zoom", ProcessListKind.Fullscreen, ProcessListProblem.InAnotherList, ProcessListKind.Meeting),
        ]);
    }

    /// <summary>
    /// Settings that follow the rules are left as they are and nothing is reported.
    /// </summary>
    [Fact]
    public void Enforce_SettingsFollowingTheRules_AreLeftAlone()
    {
        // Act
        var result = ProcessListRules.Enforce(PetSettings.Default);

        // Assert
        result.Settings.Should().BeSameAs(PetSettings.Default);
        result.Removed.Should().BeEmpty();
    }
}
