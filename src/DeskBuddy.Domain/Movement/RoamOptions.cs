namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Tuning of the free-roaming mode.
/// </summary>
public sealed class RoamOptions
{
    /// <summary>
    /// Gets or sets how far from horizontal, in degrees, a roaming walk may head up or down. Walking straight up or
    /// down looks wrong for a sprite that walks sideways.
    /// </summary>
    public double HeadingSpreadDegrees { get; set; } = 60;

    /// <summary>
    /// Gets or sets the chance, from 0 to 1, that a walk heads for the mouse pointer instead of wandering.
    /// </summary>
    public double ChaseChance { get; set; } = 0.25;

    /// <summary>
    /// Gets or sets the shortest time between two chases, so the pet never becomes a nuisance.
    /// </summary>
    public TimeSpan ChaseCooldown { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets how close, in device-independent pixels, the pet's center gets to the pointer before it stops.
    /// </summary>
    public double ArriveDistance { get; set; } = 120;

    /// <summary>
    /// Gets or sets how much faster than a normal walk the pet runs after the pointer.
    /// </summary>
    public double ChaseSpeedFactor { get; set; } = 1.5;
}
