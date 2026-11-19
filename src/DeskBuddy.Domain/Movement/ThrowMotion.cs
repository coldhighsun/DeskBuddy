namespace DeskBuddy.Domain.Movement;

/// <summary>
/// The sideways part of the flight of a thrown pet: the air slows it down, and it bounces back, slower, off the left and
/// right ends of the range it may take. The fall itself is <see cref="FreeFall"/>.
/// </summary>
/// <param name="X">
/// The left of the pet window.
/// </param>
/// <param name="Velocity">
/// The sideways speed in device-independent pixels per second, positive to the right.
/// </param>
public readonly record struct ThrowState(double X, double Velocity);

/// <summary>
/// Pure physics for the sideways flight of a thrown pet.
/// </summary>
public static class ThrowMotion
{
    /// <summary>
    /// Speeds below this, in device-independent pixels per second, count as standing still sideways.
    /// </summary>
    private const double RestSpeed = 5;

    /// <summary>
    /// Advances the sideways flight by one time step.
    /// </summary>
    /// <param name="state">
    /// The state before the step.
    /// </param>
    /// <param name="minX">
    /// The leftmost the pet window may go.
    /// </param>
    /// <param name="maxX">
    /// The rightmost the pet window may go.
    /// </param>
    /// <param name="elapsed">
    /// The time since the previous step.
    /// </param>
    /// <param name="options">
    /// The throw tuning.
    /// </param>
    /// <returns>
    /// The state after the step; never outside <paramref name="minX"/>..<paramref name="maxX"/>.
    /// </returns>
    public static ThrowState Step(ThrowState state, double minX, double maxX, TimeSpan elapsed, PetBehaviorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        maxX = Math.Max(minX, maxX);
        var seconds = elapsed.TotalSeconds;
        var velocity = state.Velocity * Math.Exp(-options.ThrowAirDrag * seconds);
        if (Math.Abs(velocity) < RestSpeed)
        {
            velocity = 0;
        }

        var x = state.X + (velocity * seconds);
        if (x < minX)
        {
            x = minX;
            velocity = Math.Abs(velocity) * options.ThrowBounce;
        }
        else if (x > maxX)
        {
            x = maxX;
            velocity = -Math.Abs(velocity) * options.ThrowBounce;
        }

        return new ThrowState(x, velocity);
    }
}
