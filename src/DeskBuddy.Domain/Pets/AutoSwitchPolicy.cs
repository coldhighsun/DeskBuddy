namespace DeskBuddy.Domain.Pets;

/// <summary>
/// Decides when the pet is changed for another one by itself: once per <see cref="Interval"/> while the policy is
/// enabled, and only at a moment when changing would not interrupt anything. It knows nothing about any particular pet;
/// <see cref="PickNext"/> chooses among whatever names it is given.
/// </summary>
/// <param name="timeProvider">The time source.</param>
public sealed class AutoSwitchPolicy(TimeProvider timeProvider)
{
    /// <summary>
    /// When the next change is due; <see langword="null"/> while disabled or until the policy is first asked.
    /// </summary>
    private DateTimeOffset? _nextAt;

    /// <summary>
    /// The time between two changes.
    /// </summary>
    private TimeSpan _interval = Settings.PetSettings.AutoSwitchIntervals[0];

    /// <summary>
    /// Guards the schedule, which is set from the thread that applies the settings, from the one that asks and when the
    /// pet is changed by hand.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// Gets or sets a value indicating whether the pet is changed at all.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the time between two changes; setting a different value starts the wait afresh.
    /// </summary>
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            lock (_gate)
            {
                if (value != _interval)
                {
                    _interval = value;
                    _nextAt = null;
                }
            }
        }
    }

    /// <summary>
    /// Tells whether a pet in a state may be changed for another without interrupting anything: only standing and
    /// walking qualify, not sleeping, a message, a fall, a scenario or being held.
    /// </summary>
    /// <param name="state">The state the pet is in.</param>
    /// <returns><see langword="true"/> if the pet may be changed now.</returns>
    public static bool CanSwitchIn(PetState state)
    {
        return state is PetState.Idle or PetState.Walk;
    }

    /// <summary>
    /// Asks whether the pet should be changed now.
    /// </summary>
    /// <param name="canSwitch"><see langword="true"/> while a change would not interrupt anything.</param>
    /// <returns>
    /// <see langword="true"/> once per interval; a change that comes due while <paramref name="canSwitch"/> is false is
    /// made as soon as it becomes true.
    /// </returns>
    public bool Update(bool canSwitch)
    {
        if (!IsEnabled)
        {
            lock (_gate)
            {
                _nextAt = null;
            }

            return false;
        }

        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();

            // The first change comes after a full interval rather than the moment the option is switched on.
            _nextAt ??= now + _interval;
            if (!canSwitch || now < _nextAt)
            {
                return false;
            }

            _nextAt = now + _interval;
            return true;
        }
    }

    /// <summary>
    /// Postpones the next change by a full interval, for when a change was due but nothing could be done about it and for
    /// when the pet was changed by hand.
    /// </summary>
    public void Postpone()
    {
        lock (_gate)
        {
            _nextAt = IsEnabled ? timeProvider.GetUtcNow() + _interval : null;
        }
    }

    /// <summary>
    /// Chooses the pet to change to.
    /// </summary>
    /// <param name="names">The names of the pets that can be shown.</param>
    /// <param name="current">The name of the pet on screen, compared ignoring case.</param>
    /// <param name="random">The random source.</param>
    /// <returns>A random name other than <paramref name="current"/>, or <see langword="null"/> if there is none.</returns>
    public static string? PickNext(IReadOnlyList<string> names, string current, Random random)
    {
        var others = names.Where(name => !string.Equals(name, current, StringComparison.OrdinalIgnoreCase)).ToList();
        return others.Count == 0 ? null : others[random.Next(others.Count)];
    }
}
