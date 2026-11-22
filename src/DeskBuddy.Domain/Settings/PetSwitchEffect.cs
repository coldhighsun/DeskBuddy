namespace DeskBuddy.Domain.Settings;

/// <summary>
/// The animation played when the pet on screen is replaced by another one.
/// </summary>
public enum PetSwitchEffect
{
    /// <summary>
    /// A different effect, picked at random, every time a pet is switched.
    /// </summary>
    Random,

    /// <summary>
    /// A flying saucer beams the old pet up and beams the new one down.
    /// </summary>
    Ufo,

    /// <summary>
    /// A crane claw lifts the old pet out of the screen and lowers the new one in.
    /// </summary>
    Claw,

    /// <summary>
    /// A puff of smoke hides the swap.
    /// </summary>
    Smoke,

    /// <summary>
    /// A beam of light scans the old pet away and the new one into view.
    /// </summary>
    Beam,
}
