namespace DeskBuddy.Domain.Pets;

/// <summary>
/// Says which state to show instead when a pet has no animation for the one asked for, so a pet that predates a
/// state still looks sensible instead of falling all the way back to standing still.
/// </summary>
public static class PetStateFallback
{
    /// <summary>
    /// Gets the state to try instead of the given one.
    /// </summary>
    /// <param name="state">
    /// The state a pet lacks.
    /// </param>
    /// <returns>
    /// The next state to try, or <see langword="null"/> if there is none (the pet then shows Idle).
    /// </returns>
    public static PetState? Of(PetState state)
    {
        return state switch
        {
            PetState.NightCoding => PetState.Coding,
            PetState.Building => PetState.Coding,
            PetState.BuildDone => PetState.Happy,
            PetState.GitPush => PetState.Happy,
            PetState.Welcome => PetState.Happy,
            PetState.Charging => PetState.Happy,
            PetState.Meal => PetState.Happy,
            PetState.ClockOut => PetState.Happy,
            PetState.Chime => PetState.Happy,
            PetState.Festive => PetState.Happy,
            PetState.Dizzy => PetState.Tired,
            PetState.Meeting => PetState.Focus,
            PetState.Chatting => PetState.Happy,
            PetState.LowBattery => PetState.Tired,
            PetState.Fall => PetState.Drag,
            PetState.Glide => PetState.Fall,
            PetState.Climb => PetState.Walk,
            PetState.ClimbDown => PetState.Climb,
            PetState.Ceiling => PetState.Walk,
            PetState.Hop => PetState.Walk,
            _ => null,
        };
    }
}
