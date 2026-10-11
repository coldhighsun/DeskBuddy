namespace DeskBuddy.Domain.Scenarios;

/// <summary>
/// Which scenarios the user has switched on. Shared and updated from the settings, like the other option objects.
/// </summary>
public sealed class ScenarioSwitches
{
    /// <summary>
    /// Gets or sets a value indicating whether the pet acts out scenarios at all.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the names (see <see cref="PetState"/>) of the scenarios that are switched off.
    /// </summary>
    public IReadOnlyCollection<string> Disabled { get; set; } = [];

    /// <summary>
    /// Tells whether a scenario may be acted out.
    /// </summary>
    /// <param name="scenario">
    /// The scenario, identified by the state it shows.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if scenarios are on and this one has not been switched off.
    /// </returns>
    public bool Allows(PetState scenario)
    {
        return IsEnabled && !Disabled.Contains(scenario.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
