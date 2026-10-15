using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.Domain.Tests.Scenarios;

/// <summary>
/// Tests for <see cref="ScenarioSwitches"/>.
/// </summary>
public class ScenarioSwitchesTests
{
    /// <summary>
    /// Everything is allowed until the user switches something off.
    /// </summary>
    [Fact]
    public void Allows_Default_IsTrueForEveryScenario()
    {
        // Arrange
        var switches = new ScenarioSwitches();

        // Assert
        ScenarioCatalog.All.Should().OnlyContain(scenario => switches.Allows(scenario));
    }

    /// <summary>
    /// A scenario that is switched off is not allowed; the others still are.
    /// </summary>
    [Fact]
    public void Allows_ScenarioDisabled_IsFalseOnlyForThatOne()
    {
        // Arrange
        var switches = new ScenarioSwitches { Disabled = ["buildDone"] };

        // Assert
        switches.Allows(PetState.BuildDone).Should().BeFalse();
        switches.Allows(PetState.Building).Should().BeTrue();
    }

    /// <summary>
    /// With the master switch off nothing is allowed.
    /// </summary>
    [Fact]
    public void Allows_MasterSwitchOff_IsFalseForEveryScenario()
    {
        // Arrange
        var switches = new ScenarioSwitches { IsEnabled = false };

        // Assert
        ScenarioCatalog.All.Should().OnlyContain(scenario => !switches.Allows(scenario));
    }
}
