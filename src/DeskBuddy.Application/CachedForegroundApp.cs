using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.Application;

/// <summary>
/// Remembers which application is in front for a short while, so the several parts that ask in the same moment cost one
/// system query instead of one each.
/// </summary>
/// <param name="inner">
/// The detector that does the real query.
/// </param>
/// <param name="timeProvider">
/// The time source.
/// </param>
/// <param name="lifetime">
/// How long an answer is reused.
/// </param>
public sealed class CachedForegroundApp(IForegroundAppDetector inner, TimeProvider timeProvider, TimeSpan lifetime)
    : IForegroundAppDetector
{
    /// <summary>
    /// The last answer.
    /// </summary>
    private string? _name;

    /// <summary>
    /// When the last answer was given, or <see langword="null"/> if there was none.
    /// </summary>
    private DateTimeOffset? _readAt;

    /// <inheritdoc />
    public string? GetForegroundProcessName()
    {
        var now = timeProvider.GetUtcNow();
        if (_readAt is { } readAt && now - readAt < lifetime)
        {
            return _name;
        }

        _name = inner.GetForegroundProcessName();
        _readAt = now;
        return _name;
    }
}
