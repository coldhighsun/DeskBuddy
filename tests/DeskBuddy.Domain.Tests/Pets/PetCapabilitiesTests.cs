using AwesomeAssertions;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.Domain.Tests.Pets;

/// <summary>
/// Tests for <see cref="PetCapabilities"/>.
/// </summary>
public class PetCapabilitiesTests
{
    /// <summary>
    /// A pet with nothing but the required Idle animation has none of the optional abilities.
    /// </summary>
    [Fact]
    public void Of_OnlyIdle_HasNoOptionalAbilities()
    {
        // Arrange
        var pet = CreatePet();

        // Act
        var capabilities = PetCapabilities.Of(pet);

        // Assert
        capabilities.NativeStateCount.Should().Be(1);
        capabilities.TotalStateCount.Should().Be(Enum.GetValues<PetState>().Length);
        capabilities.CanLookAround.Should().BeFalse();
        capabilities.Edges.Should().Be(new StateCoverage(0, 4));
        capabilities.Development.Should().Be(new StateCoverage(0, 6));
        capabilities.Scenarios.Should().Be(new StateCoverage(0, 9));
        capabilities.Edges.IsSupported.Should().BeFalse();
    }

    /// <summary>
    /// Each group counts only the states of that group the pet defines.
    /// </summary>
    [Fact]
    public void Of_SomeStatesDefined_CountsEachGroupSeparately()
    {
        // Arrange
        var pet = CreatePet(PetState.Climb, PetState.Hang, PetState.Coding, PetState.Meal, PetState.Walk);

        // Act
        var capabilities = PetCapabilities.Of(pet);

        // Assert
        capabilities.NativeStateCount.Should().Be(6);
        capabilities.Edges.Should().Be(new StateCoverage(2, 4));
        capabilities.Development.Should().Be(new StateCoverage(1, 6));
        capabilities.Scenarios.Should().Be(new StateCoverage(1, 9));
        capabilities.Edges.IsSupported.Should().BeTrue();
    }

    /// <summary>
    /// A pet with pictures for looking in a direction can follow the mouse.
    /// </summary>
    [Fact]
    public void Of_GazePicturesDefined_CanLookAround()
    {
        // Arrange
        var gaze = new Dictionary<GazeDirection, PetAnimation>
        {
            [GazeDirection.Left] = new(1, ["left.png"]),
        };
        var pet = new Pet("test", 64, 64, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = new(2, ["idle.png"]) }, gaze);

        // Act
        var capabilities = PetCapabilities.Of(pet);

        // Assert
        capabilities.CanLookAround.Should().BeTrue();
    }

    /// <summary>
    /// A state the pet only borrows from another state does not count as defined.
    /// </summary>
    [Fact]
    public void Of_StateOnlyBorrowed_IsNotCounted()
    {
        // Arrange: Climb falls back to Walk, but the pet draws only Walk.
        var pet = CreatePet(PetState.Walk);

        // Act
        var capabilities = PetCapabilities.Of(pet);

        // Assert
        capabilities.Edges.Supported.Should().Be(0);
    }

    /// <summary>
    /// Creating capabilities without a pet is a programming error.
    /// </summary>
    [Fact]
    public void Of_NoPet_Throws()
    {
        // Act
        var act = () => PetCapabilities.Of(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Creates a pet with an Idle animation and the given further states.
    /// </summary>
    /// <param name="states">
    /// The states to define besides Idle.
    /// </param>
    /// <returns>
    /// The pet.
    /// </returns>
    private static Pet CreatePet(params PetState[] states)
    {
        var animations = new Dictionary<PetState, PetAnimation> { [PetState.Idle] = new(2, ["idle.png"]) };
        foreach (var state in states)
        {
            animations[state] = new PetAnimation(2, [$"{state}.png"]);
        }

        return new Pet("test", 64, 64, animations);
    }
}
