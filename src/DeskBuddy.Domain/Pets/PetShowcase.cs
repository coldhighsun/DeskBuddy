namespace DeskBuddy.Domain.Pets;

/// <summary>
/// The states shown when a pet is previewed, so users can see what a pet looks like doing everyday things.
/// </summary>
public static class PetShowcase
{
    /// <summary>
    /// Gets the states worth showing, in the order they are shown.
    /// </summary>
    public static IReadOnlyList<PetState> States { get; } =
    [
        PetState.Idle,
        PetState.Walk,
        PetState.Sleep,
        PetState.Happy,
        PetState.Alert,
        PetState.Tired,
        PetState.Dizzy,
        PetState.Climb,
        PetState.Coding,
        PetState.Meal,
    ];

    /// <summary>
    /// Picks the showcase states a pet draws itself. States the pet only borrows from another state are left out, so the
    /// preview shows what the pet really has.
    /// </summary>
    /// <param name="pet">
    /// The pet.
    /// </param>
    /// <returns>
    /// The states, in showcase order; always starts with <see cref="PetState.Idle"/>, which every pet defines.
    /// </returns>
    public static IReadOnlyList<PetState> NativeStatesOf(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        return [.. States.Where(pet.Animations.ContainsKey)];
    }
}
