namespace DeskBuddy.Domain.Pets;

/// <summary>
/// The states shown when a pet is previewed, so users can see what a pet looks like doing everyday things.
/// </summary>
public static class PetShowcase
{
    /// <summary>
    /// Gets the states worth showing. The order is only the order they are listed in; a preview plays them in a random order
    /// (see <see cref="RandomOrder"/>), after Idle.
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

    /// <summary>
    /// Puts states in a random order, for a preview that does not play them the same way every time.
    /// </summary>
    /// <param name="states">
    /// The states to put in order; each comes out exactly once.
    /// </param>
    /// <param name="random">
    /// The source of randomness.
    /// </param>
    /// <param name="first">
    /// A state that has to come first (if it is among <paramref name="states"/>), or <see langword="null"/> for none.
    /// </param>
    /// <param name="notFirst">
    /// A state that must not come first (normally the one that was shown last, so it is not shown twice in a row), or
    /// <see langword="null"/> for none. Ignored when there is nothing else to put first.
    /// </param>
    /// <returns>
    /// The states in the new order.
    /// </returns>
    public static IReadOnlyList<PetState> RandomOrder(IReadOnlyList<PetState> states, Random random, PetState? first = null, PetState? notFirst = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(random);

        var rest = states.Where(state => state != first).ToList();
        for (var i = rest.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (rest[i], rest[j]) = (rest[j], rest[i]);
        }

        if (first is { } head && states.Contains(head))
        {
            return [head, .. rest];
        }

        // Nothing is forced to the front: do not start with the state that was shown last.
        if (rest.Count > 1 && rest[0] == notFirst)
        {
            var swap = random.Next(1, rest.Count);
            (rest[0], rest[swap]) = (rest[swap], rest[0]);
        }

        return rest;
    }
}
