using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Domain.Activity;

/// <summary>
/// Decides whether the user is developing: a development tool is in front and the user has recently used keyboard or
/// mouse. Once that holds it keeps holding for a grace period after the conditions lapse, so a quick look at a browser
/// or a short pause to think does not flip the answer back and forth.
/// </summary>
/// <param name="options">
/// The timings: how recent input must be and how long the grace period lasts.
/// </param>
/// <param name="timeProvider">
/// The time source.
/// </param>
public sealed class DevActivityPolicy(PetBehaviorOptions options, TimeProvider timeProvider)
{
    /// <summary>
    /// Whether the user is considered to be developing right now.
    /// </summary>
    private bool _isDeveloping;

    /// <summary>
    /// The moment the conditions first lapsed while developing, or <see langword="null"/> while they hold.
    /// </summary>
    private DateTimeOffset? _unmetSince;

    /// <summary>
    /// The moment the current development session began, or <see langword="null"/> when there is none.
    /// </summary>
    private DateTimeOffset? _sessionSince;

    /// <summary>
    /// The last moment the user counted as developing, or <see langword="null"/> if never.
    /// </summary>
    private DateTimeOffset? _lastDevelopingAt;

    /// <summary>
    /// Gets or sets a value indicating whether development is detected at all. While off, <see cref="Update"/> always
    /// reports <see langword="false"/>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the names of the processes (without <c>.exe</c>) that count as development tools.
    /// </summary>
    public IReadOnlyList<string> Processes { get; set; } = [];

    /// <summary>
    /// Gets how long the current development session has lasted, bridging looks elsewhere and pauses shorter than
    /// <see cref="PetBehaviorOptions.DevSessionBreak"/>; zero when the user is not developing.
    /// </summary>
    public TimeSpan DevelopingFor => _isDeveloping && _sessionSince is { } since ? timeProvider.GetUtcNow() - since : TimeSpan.Zero;

    /// <summary>
    /// Gets how long ago the user last counted as developing: zero while they are, <see langword="null"/> if they
    /// never have since the application started.
    /// </summary>
    public TimeSpan? SinceDeveloping => _lastDevelopingAt is { } last ? timeProvider.GetUtcNow() - last : null;

    /// <summary>
    /// Re-evaluates whether the user is developing.
    /// </summary>
    /// <param name="foregroundProcess">
    /// The name of the process owning the foreground window without <c>.exe</c>, or <see langword="null"/> if unknown.
    /// </param>
    /// <param name="userIdle">
    /// How long the user has had no keyboard or mouse input.
    /// </param>
    /// <returns>
    /// <see langword="true"/> while a development tool has been in front with recent input, and for the grace period
    /// after that stops being so.
    /// </returns>
    public bool Update(string? foregroundProcess, TimeSpan userIdle)
    {
        if (!IsEnabled)
        {
            _isDeveloping = false;
            _unmetSince = null;
            _sessionSince = null;
            _lastDevelopingAt = null;
            return false;
        }

        var conditionsMet = foregroundProcess is not null
            && userIdle < options.DevActiveWindow
            && Processes.Contains(foregroundProcess, StringComparer.OrdinalIgnoreCase);

        var now = timeProvider.GetUtcNow();
        if (conditionsMet)
        {
            _isDeveloping = true;
            _unmetSince = null;
        }
        else if (_isDeveloping)
        {
            _unmetSince ??= now;
            if (now - _unmetSince >= options.DevExitGrace)
            {
                _isDeveloping = false;
                _unmetSince = null;
            }
        }

        if (_isDeveloping)
        {
            // A pause that was long enough to be a real break starts a new session.
            if (_sessionSince is null || (_lastDevelopingAt is { } last && now - last >= options.DevSessionBreak))
            {
                _sessionSince = now;
            }

            _lastDevelopingAt = now;
        }

        return _isDeveloping;
    }
}
