namespace DeskBuddy.Domain.Scenarios;

/// <summary>
/// Picks the one ambient state to act out when several things are going on at once, by a fixed ranking.
/// </summary>
/// <param name="priority">
/// The ambient states, most important first. A state that is not listed is never picked.
/// </param>
public sealed class ScenarioSelector(IReadOnlyList<PetState> priority)
{
    /// <summary>
    /// Chooses among the states that are currently active.
    /// </summary>
    /// <param name="active">
    /// The ambient states that apply right now, in any order.
    /// </param>
    /// <returns>
    /// The highest-ranked state that is active, or <see langword="null"/> if none is.
    /// </returns>
    public PetState? Pick(IReadOnlyCollection<PetState> active)
    {
        ArgumentNullException.ThrowIfNull(active);

        foreach (var state in priority)
        {
            if (active.Contains(state))
            {
                return state;
            }
        }

        return null;
    }
}
