namespace DeskBuddy.Domain.Movement;

/// <summary>
/// The path of a hop: how high above where it walks the pet is at each moment of the hop.
/// </summary>
public static class HopArc
{
    /// <summary>
    /// Tells whether a hop has been played out.
    /// </summary>
    /// <param name="elapsed">
    /// The time since the hop began.
    /// </param>
    /// <param name="duration">
    /// How long the hop lasts.
    /// </param>
    /// <returns>
    /// <see langword="true"/> once <paramref name="elapsed"/> has reached <paramref name="duration"/>.
    /// </returns>
    public static bool IsOver(TimeSpan elapsed, TimeSpan duration)
    {
        return elapsed >= duration;
    }

    /// <summary>
    /// Works out how high the pet is above its walking line, in a parabola that starts and ends on the line.
    /// </summary>
    /// <param name="elapsed">
    /// The time since the hop began; outside of the hop the pet is on the line.
    /// </param>
    /// <param name="duration">
    /// How long the hop lasts.
    /// </param>
    /// <param name="petHeight">
    /// The height of the pet.
    /// </param>
    /// <param name="heightRatio">
    /// How high the top of the hop is, as a fraction of <paramref name="petHeight"/>.
    /// </param>
    /// <returns>
    /// The height above the walking line, zero or more, in the unit of <paramref name="petHeight"/>.
    /// </returns>
    public static double Offset(TimeSpan elapsed, TimeSpan duration, double petHeight, double heightRatio)
    {
        if (duration <= TimeSpan.Zero || elapsed <= TimeSpan.Zero || elapsed >= duration)
        {
            return 0;
        }

        var progress = elapsed / duration;
        return Math.Max(0, 4 * progress * (1 - progress) * petHeight * heightRatio);
    }
}
