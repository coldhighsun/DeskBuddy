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
    /// Every state comes out exactly once, whatever the random numbers are.
    /// </summary>
    [Fact]
    public void RandomOrder_AnyStates_ReturnsEachOnce()
    {
        // Arrange
        PetState[] states = [PetState.Idle, PetState.Walk, PetState.Sleep, PetState.Happy];

        // Act
        var order = PetShowcase.RandomOrder(states, new Random(5));

        // Assert
        order.Should().BeEquivalentTo(states);
        order.Should().HaveCount(states.Length);
    }

    /// <summary>
    /// A state asked to come first does, and the others follow in an order that depends on the random numbers.
    /// </summary>
    [Fact]
    public void RandomOrder_FirstGiven_StartsWithItAndVariesTheRest()
    {
        // Arrange
        PetState[] states = [PetState.Idle, PetState.Walk, PetState.Sleep, PetState.Happy, PetState.Alert];
        var random = new Random(11);

        // Act
        var orders = Enumerable.Range(0, 20).Select(_ => PetShowcase.RandomOrder(states, random, first: PetState.Idle)).ToList();

        // Assert
        orders.Should().OnlyContain(order => order[0] == PetState.Idle);
        orders.Select(order => string.Join(',', order)).Distinct().Count().Should().BeGreaterThan(1);
    }

    /// <summary>
    /// A state that must not come first does not, as long as there is another one to put there.
    /// </summary>
    [Fact]
    public void RandomOrder_NotFirstGiven_DoesNotStartWithIt()
    {
        // Arrange
        PetState[] states = [PetState.Walk, PetState.Sleep, PetState.Happy];
        var random = new Random(2);

        // Act
        var orders = Enumerable.Range(0, 50).Select(_ => PetShowcase.RandomOrder(states, random, notFirst: PetState.Walk)).ToList();

        // Assert
        orders.Should().OnlyContain(order => order[0] != PetState.Walk);
    }

    /// <summary>
    /// With only the state that must not come first there is nothing else to put first, so it does.
    /// </summary>
    [Fact]
    public void RandomOrder_OnlyTheExcludedState_StillReturnsIt()
    {
        // Act
        var order = PetShowcase.RandomOrder([PetState.Idle], new Random(1), notFirst: PetState.Idle);

        // Assert
        order.Should().Equal(PetState.Idle);
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
