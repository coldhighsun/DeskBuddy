namespace DeskBuddy.Application.Sensors;

/// <summary>
/// Lists the running processes at most once per interval and hands out the last list in between, as walking all the
/// processes of the machine every second is more work than a desktop pet should cause.
/// </summary>
/// <param name="inner">
/// The list that does the real query.
/// </param>
/// <param name="timeProvider">
/// The time source.
/// </param>
/// <param name="interval">
/// The shortest time between two real queries.
/// </param>
public sealed class ThrottledProcessList(IProcessList inner, TimeProvider timeProvider, TimeSpan interval) : IProcessList
{
    /// <summary>
    /// The last list.
    /// </summary>
    private IReadOnlySet<string> _names = new HashSet<string>();

    /// <summary>
    /// When the last list was taken, or <see langword="null"/> if there was none.
    /// </summary>
    private DateTimeOffset? _readAt;

    /// <inheritdoc />
    public IReadOnlySet<string> GetNames()
    {
        var now = timeProvider.GetUtcNow();
        if (_readAt is { } readAt && now - readAt < interval)
        {
            return _names;
        }

        _names = inner.GetNames();
        _readAt = now;
        return _names;
    }
}
