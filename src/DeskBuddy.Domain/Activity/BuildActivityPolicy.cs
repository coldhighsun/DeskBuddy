using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Activity;

/// <summary>
/// What a call to <see cref="BuildActivityPolicy.Update"/> found.
/// </summary>
public enum BuildChange
{
    /// <summary>
    /// Nothing worth reporting happened.
    /// </summary>
    None,

    /// <summary>
    /// A build was recognized.
    /// </summary>
    Started,

    /// <summary>
    /// A build that had lasted long enough to be worth a cheer is over.
    /// </summary>
    Finished,
}

/// <summary>
/// Recognizes a build: a compiler or build tool is running <em>and</em> the processor is busy, for a few seconds. The
/// processor condition matters because tools such as <c>dotnet</c> also run idle as language servers. A build ends
/// when either condition has lapsed for a grace period, so short pauses inside a build do not end it. It cannot tell
/// whether the build succeeded.
/// </summary>
/// <param name="options">
/// The thresholds and timings.
/// </param>
/// <param name="timeProvider">
/// The time source.
/// </param>
public sealed class BuildActivityPolicy(PetBehaviorOptions options, TimeProvider timeProvider)
{
    /// <summary>
    /// The moment the start conditions first held, while waiting to be sure; <see langword="null"/> otherwise.
    /// </summary>
    private DateTimeOffset? _candidateSince;

    /// <summary>
    /// The moment the current build began, or <see langword="null"/> if none is running.
    /// </summary>
    private DateTimeOffset? _buildingSince;

    /// <summary>
    /// The moment the build first looked paused, or <see langword="null"/> while it is going on.
    /// </summary>
    private DateTimeOffset? _pausedSince;

    /// <summary>
    /// Gets a value indicating whether a build is going on.
    /// </summary>
    public bool IsBuilding => _buildingSince is not null;

    /// <summary>
    /// Forgets any build in progress or about to be recognized, for when the readings stop coming (the scenario was
    /// switched off), so a later reading is not compared with a build from long ago.
    /// </summary>
    public void Reset()
    {
        _candidateSince = null;
        _buildingSince = null;
        _pausedSince = null;
    }

    /// <summary>
    /// Takes in the latest readings.
    /// </summary>
    /// <param name="toolRunning">
    /// Whether any build tool is running.
    /// </param>
    /// <param name="cpuPercent">
    /// The current processor load, from 0 to 100.
    /// </param>
    /// <returns>
    /// Whether a build was just recognized or just ended.
    /// </returns>
    public BuildChange Update(bool toolRunning, double cpuPercent)
    {
        var now = timeProvider.GetUtcNow();

        if (_buildingSince is not { } since)
        {
            if (!toolRunning || cpuPercent < options.BuildStartCpu)
            {
                _candidateSince = null;
                return BuildChange.None;
            }

            _candidateSince ??= now;
            if (now - _candidateSince < options.BuildStartAfter)
            {
                return BuildChange.None;
            }

            _buildingSince = _candidateSince;
            _candidateSince = null;
            _pausedSince = null;
            return BuildChange.Started;
        }

        if (toolRunning && cpuPercent >= options.BuildContinueCpu)
        {
            _pausedSince = null;
            return BuildChange.None;
        }

        _pausedSince ??= now;
        if (now - _pausedSince < options.BuildEndGrace)
        {
            return BuildChange.None;
        }

        var lasted = _pausedSince.Value - since;
        _buildingSince = null;
        _pausedSince = null;
        return lasted >= options.BuildMinDuration ? BuildChange.Finished : BuildChange.None;
    }
}
