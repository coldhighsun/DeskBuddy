namespace DeskBuddy.Domain.Chatter;

/// <summary>
/// Decides when a roaming pet says something unprompted. Remarks are rare (once per <see cref="Interval"/> at most,
/// with some jitter), never the same one twice in a row, and only while the policy is enabled and the user is free to
/// be spoken to.
/// </summary>
/// <param name="timeProvider">
/// The time source.
/// </param>
/// <param name="random">
/// The random source for the wait between remarks and for choosing one.
/// </param>
public sealed class ChatterPolicy(TimeProvider timeProvider, Random random)
{
    /// <summary>
    /// Number of different remarks; <see cref="Update"/> returns an index below this.
    /// </summary>
    public const int VariantCount = 8;

    /// <summary>
    /// The shortest time between two remarks.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The most that is added to <see cref="Interval"/> at random.
    /// </summary>
    private static readonly TimeSpan Jitter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When the next remark is due; <see langword="null"/> until the policy is first asked.
    /// </summary>
    private DateTimeOffset? _nextAt;

    /// <summary>
    /// Index of the last remark, to avoid repeating it.
    /// </summary>
    private int _last = -1;

    /// <summary>
    /// Gets or sets a value indicating whether the pet may speak at all.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Asks whether the pet should say something now.
    /// </summary>
    /// <param name="canSpeak">
    /// <see langword="true"/> while speaking would not intrude: the user is at the computer, and nothing else is on screen.
    /// </param>
    /// <returns>
    /// The index of the remark to say, or <see langword="null"/> if the pet should stay quiet. A remark that comes due
    /// while <paramref name="canSpeak"/> is false is said as soon as it becomes true.
    /// </returns>
    public int? Update(bool canSpeak)
    {
        if (!IsEnabled)
        {
            _nextAt = null;
            return null;
        }

        var now = timeProvider.GetUtcNow();

        // The first remark comes after a while rather than the moment roaming is switched on.
        _nextAt ??= now + Interval + (Jitter * random.NextDouble());
        if (!canSpeak || now < _nextAt)
        {
            return null;
        }

        _nextAt = now + Interval + (Jitter * random.NextDouble());

        var pick = random.Next(VariantCount - 1);
        _last = pick >= _last && _last >= 0 ? pick + 1 : pick;
        return _last;
    }
}
