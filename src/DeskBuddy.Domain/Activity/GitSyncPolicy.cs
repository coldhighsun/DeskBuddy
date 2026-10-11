using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Activity;

/// <summary>
/// Notices git talking to a remote. Reports each start once, and not again until a cooldown has passed, so a chain of
/// fetches does not keep the pet busy.
/// </summary>
/// <param name="options">
/// The cooldown.
/// </param>
/// <param name="timeProvider">
/// The time source.
/// </param>
public sealed class GitSyncPolicy(PetBehaviorOptions options, TimeProvider timeProvider)
{
    /// <summary>
    /// Whether git was talking to a remote at the previous update.
    /// </summary>
    private bool _wasRunning;

    /// <summary>
    /// The moment of the last report, or <see langword="null"/> if there has been none.
    /// </summary>
    private DateTimeOffset? _lastTriggeredAt;

    /// <summary>
    /// Forgets whether a helper was running, for when the readings stop coming (the scenario was switched off), so
    /// that a process already running when it is switched on again does not count as a new start.
    /// </summary>
    public void Reset()
    {
        _wasRunning = false;
    }

    /// <summary>
    /// Takes in the latest reading.
    /// </summary>
    /// <param name="remoteHelperRunning">
    /// Whether a git remote helper process is running.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when git has just started talking to a remote and the cooldown is over.
    /// </returns>
    public bool Update(bool remoteHelperRunning)
    {
        var started = remoteHelperRunning && !_wasRunning;
        _wasRunning = remoteHelperRunning;

        if (!started)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (_lastTriggeredAt is { } last && now - last < options.GitCooldown)
        {
            return false;
        }

        _lastTriggeredAt = now;
        return true;
    }
}
