namespace DeskBuddy.Domain.Scenarios;

/// <summary>
/// The situations the pet acts out by itself, each identified by the <see cref="PetState"/> it shows. The user can
/// switch each of them off.
/// </summary>
public static class ScenarioCatalog
{
    /// <summary>
    /// Gets the ranking of the scenarios that last for as long as they apply, for when several apply at once: a meeting
    /// first, as the pet must then keep still and quiet, then chatting, then a running build, then the rest.
    /// </summary>
    public static ScenarioSelector AmbientRanking { get; } = new(
        [PetState.Meeting, PetState.Chatting, PetState.Building, PetState.LowBattery, PetState.Reading, PetState.NightCoding]);

    /// <summary>
    /// Gets every scenario, in the order they are listed in the settings.
    /// </summary>
    public static IReadOnlyList<PetState> All { get; } =
    [
        PetState.Building,
        PetState.BuildDone,
        PetState.GitPush,
        PetState.NightCoding,
        PetState.Reading,
        PetState.Meeting,
        PetState.Chatting,
        PetState.LowBattery,
        PetState.Charging,
        PetState.Welcome,
        PetState.Meal,
        PetState.ClockOut,
        PetState.Chime,
        PetState.Festive,
        PetState.Dizzy,
        PetState.Hang,
    ];
}
