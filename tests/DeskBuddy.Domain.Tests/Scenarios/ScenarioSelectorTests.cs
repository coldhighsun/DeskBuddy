using AwesomeAssertions;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.Domain.Tests.Scenarios;

/// <summary>
/// Tests for <see cref="ScenarioSelector"/>.
/// </summary>
public class ScenarioSelectorTests
{
    /// <summary>
    /// The selector under test; Focus ranks above Tired, and Coding is not ranked at all.
    /// </summary>
    private readonly ScenarioSelector _selector = new([PetState.Focus, PetState.Tired]);

    /// <summary>
    /// With nothing active there is nothing to pick.
    /// </summary>
    [Fact]
    public void Pick_NothingActive_ReturnsNull()
    {
        // Act
        var picked = _selector.Pick([]);

        // Assert
        picked.Should().BeNull();
    }

    /// <summary>
    /// A single active state is picked.
    /// </summary>
    [Fact]
    public void Pick_OneActive_ReturnsIt()
    {
        // Act
        var picked = _selector.Pick([PetState.Tired]);

        // Assert
        picked.Should().Be(PetState.Tired);
    }

    /// <summary>
    /// The highest-ranked state wins whatever the order they were reported in.
    /// </summary>
    [Fact]
    public void Pick_SeveralActive_ReturnsHighestRanked()
    {
        // Act
        var picked = _selector.Pick([PetState.Tired, PetState.Focus]);

        // Assert
        picked.Should().Be(PetState.Focus);
    }

    /// <summary>
    /// A state that is not ranked is never picked.
    /// </summary>
    [Fact]
    public void Pick_OnlyUnrankedActive_ReturnsNull()
    {
        // Act
        var picked = _selector.Pick([PetState.Coding]);

        // Assert
        picked.Should().BeNull();
    }
}
