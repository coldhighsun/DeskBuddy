namespace DeskBuddy.Domain.Messages;

/// <summary>
/// Holds the message currently shown and the ones waiting behind it. Messages are shown one at a time, expire after a
/// while, and are held back (not lost) while the user must not be disturbed.
/// </summary>
public sealed class PetMessageQueue
{
    /// <summary>
    /// How long a message stays up before it fades on its own.
    /// </summary>
    private readonly TimeSpan _messageDuration;

    /// <summary>
    /// Source of the current time.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// How long <see cref="Current"/> stays up.
    /// </summary>
    private TimeSpan _currentLifetime;

    /// <summary>
    /// Messages waiting to be shown, oldest first.
    /// </summary>
    private readonly LinkedList<PetMessage> _waiting = new();

    /// <summary>
    /// When <see cref="Current"/> was put on screen.
    /// </summary>
    private DateTimeOffset _shownAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetMessageQueue"/> class.
    /// </summary>
    /// <param name="messageDuration">
    /// How long a message stays up before it fades on its own.
    /// </param>
    /// <param name="timeProvider">
    /// The time source.
    /// </param>
    public PetMessageQueue(TimeSpan messageDuration, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _messageDuration = messageDuration;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the message currently on screen, or <see langword="null"/> if none.
    /// </summary>
    public PetMessage? Current { get; private set; }

    /// <summary>
    /// Gets the number of messages waiting behind the current one.
    /// </summary>
    public int WaitingCount => _waiting.Count;

    /// <summary>
    /// Adds a message to the back of the line. It appears on a later <see cref="Update"/>.
    /// </summary>
    /// <param name="message">
    /// The message to show.
    /// </param>
    public void Enqueue(PetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _waiting.AddLast(message);
    }

    /// <summary>
    /// Advances the queue: expires the current message, puts the next one on screen, or pulls the current one back
    /// while the user must not be disturbed.
    /// </summary>
    /// <param name="suppressed">
    /// <see langword="true"/> while messages must not be shown (for example during a full-screen presentation).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <see cref="Current"/> changed.
    /// </returns>
    public bool Update(bool suppressed)
    {
        var now = _timeProvider.GetUtcNow();
        var changed = false;

        if (Current is { } current)
        {
            if (suppressed)
            {
                _waiting.AddFirst(current);
                Current = null;
                return true;
            }

            if (now - _shownAt >= _currentLifetime)
            {
                Current = null;
                changed = true;
            }
        }

        if (Current is null && !suppressed && _waiting.First is { } next)
        {
            _waiting.RemoveFirst();
            Current = next.Value;
            _shownAt = now;
            _currentLifetime = next.Value.Lifetime ?? _messageDuration;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Closes the current message. The next waiting message appears on a later <see cref="Update"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if there was a message to close.
    /// </returns>
    public bool Dismiss()
    {
        if (Current is null)
        {
            return false;
        }

        Current = null;
        return true;
    }
}
