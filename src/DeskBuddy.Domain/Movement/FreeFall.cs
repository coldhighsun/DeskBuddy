using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Movement;

/// <summary>
/// The motion of a pet that was dropped from a height: it speeds up under gravity, and once it is fast enough its
/// umbrella or parachute opens and slows it to a gentle sink.
/// </summary>
/// <param name="Y">
/// The top of the pet window.
/// </param>
/// <param name="Velocity">
/// The downward speed in device-independent pixels per second.
/// </param>
/// <param name="Opened">
/// Whether the umbrella or parachute is open.
/// </param>
/// <param name="Landed">
/// Whether the pet has reached the ground.
/// </param>
public readonly record struct FallState(double Y, double Velocity, bool Opened, bool Landed);

/// <summary>
/// Pure physics for a pet falling to the ground.
/// </summary>
public static class FreeFall
{
    /// <summary>
    /// Decides whether a dropped pet falls instead of settling at once.
    /// </summary>
    /// <param name="dropY">
    /// The top of the pet window where it was dropped.
    /// </param>
    /// <param name="landingY">
    /// The top of the pet window where it would settle.
    /// </param>
    /// <param name="minHeight">
    /// The smallest height difference that counts as a fall.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the pet is at least <paramref name="minHeight"/> above where it would settle.
    /// </returns>
    public static bool ShouldFall(double dropY, double landingY, double minHeight)
    {
        return landingY - dropY >= minHeight;
    }

    /// <summary>
    /// Advances a fall by one time step.
    /// </summary>
    /// <param name="state">
    /// The state before the step.
    /// </param>
    /// <param name="landingY">
    /// The top of the pet window where it lands.
    /// </param>
    /// <param name="elapsed">
    /// The time since the previous step.
    /// </param>
    /// <param name="options">
    /// The fall tuning.
    /// </param>
    /// <returns>
    /// The state after the step; never below <paramref name="landingY"/>.
    /// </returns>
    public static FallState Step(FallState state, double landingY, TimeSpan elapsed, PetBehaviorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var seconds = elapsed.TotalSeconds;
        var opened = state.Opened || state.Velocity >= options.FallOpenSpeed;

        var velocity = state.Velocity + (options.FallGravity * seconds);
        if (opened)
        {
            // The open canopy brakes the pet: ease down to the terminal velocity instead of snapping to it.
            var braked = Math.Max(options.FallTerminalVelocity, state.Velocity - (options.FallBrake * seconds));
            velocity = Math.Min(velocity, braked);
        }

        var y = state.Y + (velocity * seconds);
        if (y >= landingY)
        {
            return new FallState(landingY, 0, opened, true);
        }

        return new FallState(y, velocity, opened, false);
    }
}
