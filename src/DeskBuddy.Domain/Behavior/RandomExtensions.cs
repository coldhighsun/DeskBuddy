namespace DeskBuddy.Domain.Behavior;

/// <summary>
/// Helpers for drawing random values.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Picks a random duration between two bounds.
    /// </summary>
    /// <param name="random">
    /// The source of chance.
    /// </param>
    /// <param name="min">
    /// Lower bound.
    /// </param>
    /// <param name="max">
    /// Upper bound; if not greater than <paramref name="min"/>, <paramref name="min"/> is returned.
    /// </param>
    /// <returns>
    /// A duration in the range <c>[min, max)</c>.
    /// </returns>
    public static TimeSpan NextTimeSpan(this Random random, TimeSpan min, TimeSpan max)
    {
        ArgumentNullException.ThrowIfNull(random);

        return max <= min ? min : min + ((max - min) * random.NextDouble());
    }
}
