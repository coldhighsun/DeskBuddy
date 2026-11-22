namespace DeskBuddy.Domain.Settings;

/// <summary>
/// Works out which animation a pet switch plays.
/// </summary>
public static class PetSwitchEffects
{
    /// <summary>
    /// The effects that stand for themselves, that is all but <see cref="PetSwitchEffect.Random"/>; the choices of a
    /// random pick.
    /// </summary>
    private static readonly PetSwitchEffect[] Concrete =
        [PetSwitchEffect.Ufo, PetSwitchEffect.Claw, PetSwitchEffect.Smoke, PetSwitchEffect.Beam];

    /// <summary>
    /// Turns the setting into the effect to play now.
    /// </summary>
    /// <param name="setting">The effect the user chose.</param>
    /// <param name="random">The source of chance for <see cref="PetSwitchEffect.Random"/>.</param>
    /// <returns>
    /// The setting itself, or for <see cref="PetSwitchEffect.Random"/> one of the four effects; never
    /// <see cref="PetSwitchEffect.Random"/>.
    /// </returns>
    public static PetSwitchEffect Resolve(PetSwitchEffect setting, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return setting == PetSwitchEffect.Random ? Concrete[random.Next(Concrete.Length)] : setting;
    }
}
