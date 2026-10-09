namespace DeskBuddy.Domain.SystemLoad;

/// <summary>
/// Turns noisy load readings into a stable "system is strained" flag. Load must stay high for
/// <see cref="SystemLoadOptions.HighDuration"/> to set the flag, and must stay low for
/// <see cref="SystemLoadOptions.RecoverDuration"/> to clear it; readings in between change nothing.
/// </summary>
public sealed class LoadReactionPolicy
{
    /// <summary>
    /// The thresholds and durations.
    /// </summary>
    private readonly SystemLoadOptions _options;

    /// <summary>
    /// Source of the current time.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// When load first went (and stayed) high, or <see langword="null"/> if it is not currently high.
    /// </summary>
    private DateTimeOffset? _highSince;

    /// <summary>
    /// When load first went (and stayed) low, or <see langword="null"/> if it is not currently low.
    /// </summary>
    private DateTimeOffset? _lowSince;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadReactionPolicy"/> class.
    /// </summary>
    /// <param name="options">
    /// The thresholds and durations.
    /// </param>
    /// <param name="timeProvider">
    /// The time source.
    /// </param>
    public LoadReactionPolicy(SystemLoadOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets a value indicating whether the system is currently considered strained.
    /// </summary>
    public bool IsStrained { get; private set; }

    /// <summary>
    /// Feeds a new reading into the policy.
    /// </summary>
    /// <param name="snapshot">
    /// The latest load reading.
    /// </param>
    /// <returns>
    /// <see cref="IsStrained"/> after taking the reading into account.
    /// </returns>
    public bool Update(SystemLoadSnapshot snapshot)
    {
        var now = _timeProvider.GetUtcNow();

        if (!IsStrained)
        {
            var isHigh = snapshot.CpuPercent >= _options.HighCpuPercent
                || snapshot.MemoryPercent >= _options.HighMemoryPercent;
            _highSince = isHigh ? _highSince ?? now : null;

            if (_highSince is { } since && now - since >= _options.HighDuration)
            {
                IsStrained = true;
                _highSince = null;
                _lowSince = null;
            }
        }
        else
        {
            var isLow = snapshot.CpuPercent <= _options.RecoverCpuPercent
                && snapshot.MemoryPercent <= _options.RecoverMemoryPercent;
            _lowSince = isLow ? _lowSince ?? now : null;

            if (_lowSince is { } since && now - since >= _options.RecoverDuration)
            {
                IsStrained = false;
                _highSince = null;
                _lowSince = null;
            }
        }

        return IsStrained;
    }
}
