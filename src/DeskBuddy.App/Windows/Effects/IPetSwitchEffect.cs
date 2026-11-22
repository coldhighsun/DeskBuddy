namespace DeskBuddy.App.Windows;

/// <summary>
/// One animation for switching a pet: the pet on screen leaves, and after the new pet has been put in its place it arrives.
/// An effect knows nothing about which pet it is played for; it works from the measures of <see cref="EffectStage"/>.
/// </summary>
internal interface IPetSwitchEffect
{
    /// <summary>
    /// Plays the part in which the pet on screen leaves. When it ends the pet is out of sight, and whatever has to wait for
    /// the new pet (a saucer, a claw, smoke) is still in place.
    /// </summary>
    /// <param name="stage">What to draw on and move.</param>
    /// <returns>A task that completes when the pet is gone.</returns>
    Task LeaveAsync(EffectStage stage);

    /// <summary>
    /// Plays the part in which the new pet arrives, with the stage already laid out for it. When it ends the pet is shown
    /// in its own window again, and everything the effect drew is gone.
    /// </summary>
    /// <param name="stage">What to draw on and move.</param>
    /// <returns>A task that completes when the pet has arrived.</returns>
    Task EnterAsync(EffectStage stage);
}
