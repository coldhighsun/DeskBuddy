namespace DeskBuddy.Domain.Interaction;

/// <summary>
/// How the pet reacts to a click, by how many clicks the user has made in quick succession.
/// </summary>
public enum ClickTier
{
    /// <summary>
    /// A click on its own.
    /// </summary>
    OneClick = 1,

    /// <summary>
    /// A second click close behind the first.
    /// </summary>
    Repeated = 2,

    /// <summary>
    /// The user keeps clicking; the pet has had enough.
    /// </summary>
    Annoyed = 3,
}

/// <summary>
/// Counts clicks that follow each other closely, to tell a single poke from a user who will not stop.
/// </summary>
/// <param name="timeProvider">The time source.</param>
public sealed class ClickStreak(TimeProvider timeProvider)
{
    /// <summary>
    /// The longest pause between two clicks that still belong to the same streak.
    /// </summary>
    public static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// The number of clicks in a streak at which the pet is annoyed.
    /// </summary>
    public const int AnnoyedAt = 4;

    /// <summary>
    /// When the last click was made.
    /// </summary>
    private DateTimeOffset _lastClick;

    /// <summary>
    /// The clicks in the current streak.
    /// </summary>
    private int _count;

    /// <summary>
    /// Registers a click.
    /// </summary>
    /// <returns>How the pet is to react to it.</returns>
    public ClickTier Register()
    {
        var now = timeProvider.GetUtcNow();
        _count = _count > 0 && now - _lastClick <= Gap ? _count + 1 : 1;
        _lastClick = now;

        return _count switch
        {
            1 => ClickTier.OneClick,
            < AnnoyedAt => ClickTier.Repeated,
            _ => ClickTier.Annoyed,
        };
    }

    /// <summary>
    /// Forgets the clicks made so far.
    /// </summary>
    public void Reset()
    {
        _count = 0;
    }
}
