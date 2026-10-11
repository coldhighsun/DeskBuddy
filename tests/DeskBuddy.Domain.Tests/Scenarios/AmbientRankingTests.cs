using AwesomeAssertions;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.Domain.Tests.Scenarios;

/// <summary>
/// Tests for <see cref="ScenarioCatalog.AmbientRanking"/>.
/// </summary>
public class AmbientRankingTests
{
    /// <summary>
    /// A meeting wins over everything else, and a build over the remaining ones.
    /// </summary>
    [Fact]
    public void Pick_SeveralApply_MeetingThenBuildingThenTheRest()
    {
        // Act
        var all = ScenarioCatalog.AmbientRanking.Pick(
            [PetState.NightCoding, PetState.Reading, PetState.LowBattery, PetState.Building, PetState.Meeting]);
        var noMeeting = ScenarioCatalog.AmbientRanking.Pick([PetState.NightCoding, PetState.LowBattery, PetState.Building]);
        var rest = ScenarioCatalog.AmbientRanking.Pick([PetState.NightCoding, PetState.Reading]);

        // Assert
        all.Should().Be(PetState.Meeting);
        noMeeting.Should().Be(PetState.Building);
        rest.Should().Be(PetState.Reading);
    }
}
