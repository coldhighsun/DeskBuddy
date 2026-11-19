namespace DeskBuddy.Domain.Sedentary;

/// <summary>
/// Decides when to remind the user to get up. A work streak runs while the user keeps using the computer; it starts
/// over once they have been away for <see cref="SedentaryOptions.RestThreshold"/>. Reminders repeat every
/// <see cref="SedentaryOptions.ReminderAfter"/> for as long as the streak continues.
/// </summary>
public sealed class SedentaryReminderPolicy
{
    /// <summary>
    /// The timings.
    /// </summary>
    private readonly SedentaryOptions _options;

    /// <summary>
    /// Source of the current time.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// When the current work streak began, or <see langword="null"/> if the user is resting.
    /// </summary>
    private DateTimeOffset? _streakStart;

    /// <summary>
    /// When the next reminder is due; meaningful only while a streak runs.
    /// </summary>
    private DateTimeOffset _dueAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="SedentaryReminderPolicy"/> class.
    /// </summary>
    /// <param name="options">
    /// The timings.
    /// </param>
    /// <param name="timeProvider">
    /// The time source.
    /// </param>
    public SedentaryReminderPolicy(SedentaryOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets how long the current work streak has lasted; <see cref="TimeSpan.Zero"/> while the user is resting.
    /// </summary>
    public TimeSpan StreakDuration =>
        _streakStart is { } start ? _timeProvider.GetUtcNow() - start : TimeSpan.Zero;

    /// <summary>
    /// Feeds the latest user presence into the policy.
    /// </summary>
    /// <param name="userIdle">
    /// How long the user has had no keyboard or mouse input.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a reminder is due now. It returns <see langword="true"/> once per reminder, not on
    /// every call.
    /// </returns>
    public bool Update(TimeSpan userIdle)
    {
        if (userIdle >= _options.RestThreshold)
        {
            _streakStart = null;
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        if (_streakStart is null)
        {
            _streakStart = now;
            _dueAt = now + _options.ReminderAfter;
            return false;
        }

        if (now < _dueAt)
        {
            return false;
        }

        _dueAt = now + _options.ReminderAfter;
        return true;
    }

    /// <summary>
    /// Postpones the next reminder by <see cref="SedentaryOptions.SnoozeDuration"/>, in response to the user asking
    /// to be reminded later.
    /// </summary>
    public void Snooze()
    {
        if (_streakStart is not null)
        {
            _dueAt = _timeProvider.GetUtcNow() + _options.SnoozeDuration;
        }
    }
}
