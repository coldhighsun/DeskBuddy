namespace DeskBuddy.Domain.Pets;

/// <summary>
/// How many of the states in a group a pet draws itself, as opposed to borrowing another state's pictures.
/// </summary>
/// <param name="Supported">
/// The number of states in the group the pet defines pictures for.
/// </param>
/// <param name="Total">
/// The number of states in the group.
/// </param>
public readonly record struct StateCoverage(int Supported, int Total)
{
    /// <summary>
    /// Gets a value indicating whether the pet defines at least one state of the group.
    /// </summary>
    public bool IsSupported => Supported > 0;
}

/// <summary>
/// What a pet can do, worked out from the states it defines, so pets can be compared without knowing anything about a
/// particular pet.
/// </summary>
/// <param name="NativeStateCount">
/// The number of states the pet defines pictures for.
/// </param>
/// <param name="TotalStateCount">
/// The number of states there are.
/// </param>
/// <param name="CanLookAround">
/// Whether the pet's eyes follow the mouse pointer.
/// </param>
/// <param name="Edges">
/// Climbing the sides of the screen and hanging from its top.
/// </param>
/// <param name="Development">
/// Keeping the user company while they develop.
/// </param>
/// <param name="Scenarios">
/// Acting out everyday situations such as a meeting, a meal or a low battery.
/// </param>
public sealed record PetCapabilities(
    int NativeStateCount,
    int TotalStateCount,
    bool CanLookAround,
    StateCoverage Edges,
    StateCoverage Development,
    StateCoverage Scenarios)
{
    /// <summary>
    /// The states of climbing along the edges of the screen.
    /// </summary>
    private static readonly PetState[] EdgeStates = [PetState.Climb, PetState.ClimbDown, PetState.Hang, PetState.Ceiling];

    /// <summary>
    /// The states of keeping the user company while they develop.
    /// </summary>
    private static readonly PetState[] DevelopmentStates =
    [
        PetState.Coding,
        PetState.Building,
        PetState.BuildDone,
        PetState.GitPush,
        PetState.NightCoding,
        PetState.Reading,
    ];

    /// <summary>
    /// The states of everyday situations.
    /// </summary>
    private static readonly PetState[] ScenarioStates =
    [
        PetState.Welcome,
        PetState.Meeting,
        PetState.LowBattery,
        PetState.Charging,
        PetState.Meal,
        PetState.ClockOut,
        PetState.Chime,
        PetState.Festive,
        PetState.Dizzy,
    ];

    /// <summary>
    /// Works out what a pet can do.
    /// </summary>
    /// <param name="pet">
    /// The pet.
    /// </param>
    /// <returns>
    /// The capabilities of the pet.
    /// </returns>
    public static PetCapabilities Of(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        return new PetCapabilities(
            pet.Animations.Count,
            Enum.GetValues<PetState>().Length,
            pet.CanLookAround,
            Coverage(pet, EdgeStates),
            Coverage(pet, DevelopmentStates),
            Coverage(pet, ScenarioStates));
    }

    /// <summary>
    /// Counts the states of a group that a pet defines.
    /// </summary>
    /// <param name="pet">
    /// The pet.
    /// </param>
    /// <param name="states">
    /// The states of the group.
    /// </param>
    /// <returns>
    /// The coverage of the group.
    /// </returns>
    private static StateCoverage Coverage(Pet pet, PetState[] states)
    {
        return new StateCoverage(states.Count(pet.Animations.ContainsKey), states.Length);
    }
}
