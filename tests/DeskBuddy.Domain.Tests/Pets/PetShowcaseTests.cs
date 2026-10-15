namespace DeskBuddy.Domain.Tests.Pets;

/// <summary>
/// Tests for <see cref="PetShowcase"/>.
/// </summary>
public class PetShowcaseTests
{
    /// <summary>
    /// Only states the pet defines are shown, in showcase order whatever order the pet lists them in.
    /// </summary>
    [Fact]
    public void NativeStatesOf_SomeStatesDefined_ReturnsThemInShowcaseOrder()
    {
        // Arrange
        var pet = CreatePet(PetState.Sleep, PetState.Walk, PetState.Meal);

        // Act
        var states = PetShowcase.NativeStatesOf(pet);

        // Assert
        states.Should().Equal(PetState.Idle, PetState.Walk, PetState.Sleep, PetState.Meal);
    }

    /// <summary>
    /// A state outside the showcase is not shown even if the pet defines it, and neither is a state the pet lacks.
    /// </summary>
    [Fact]
    public void NativeStatesOf_StateNotInShowcase_IsLeftOut()
    {
        // Arrange
        var pet = CreatePet(PetState.Fall, PetState.GitPush);

        // Act
        var states = PetShowcase.NativeStatesOf(pet);

        // Assert
        states.Should().Equal(PetState.Idle);
    }

    /// <summary>
    /// Every pet has Idle, so the preview always has something to show first.
    /// </summary>
    [Fact]
    public void NativeStatesOf_OnlyIdle_ReturnsIdle()
    {
        // Act
        var states = PetShowcase.NativeStatesOf(CreatePet());

        // Assert
        states.Should().Equal(PetState.Idle);
    }

    /// <summary>
    /// The showcase lists no state twice.
    /// </summary>
    [Fact]
    public void States_Always_HasNoDuplicates()
    {
        // Assert
        PetShowcase.States.Should().OnlyHaveUniqueItems();
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
