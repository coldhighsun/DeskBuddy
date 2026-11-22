namespace DeskBuddy.App.Windows;

/// <summary>
/// Makes the animation for a pet switch.
/// </summary>
internal static class PetSwitchEffectFactory
{
    /// <summary>
    /// Makes a new animation of a kind. Each switch gets its own, since an animation remembers what it drew between its
    /// two parts.
    /// </summary>
    /// <param name="effect">The kind of animation, which must not be <see cref="PetSwitchEffect.Random"/>: that is resolved first.</param>
    /// <returns>The animation, or <see langword="null"/> for a value that is not an animation.</returns>
    public static IPetSwitchEffect? Create(PetSwitchEffect effect)
    {
        return effect switch
        {
            PetSwitchEffect.Ufo => new UfoEffect(),
            PetSwitchEffect.Claw => new ClawEffect(),
            PetSwitchEffect.Smoke => new SmokeEffect(),
            PetSwitchEffect.Beam => new BeamEffect(),
            _ => null,
        };
    }
}
