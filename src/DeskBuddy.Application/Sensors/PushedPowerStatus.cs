using DeskBuddy.Domain.Power;

namespace DeskBuddy.Application.Sensors;

/// <summary>
/// Answers <see cref="IPowerStatusProvider"/> from what the system tells: whenever the power supply changes something
/// calls <see cref="Push"/> with the new status, so the answer is current without reading anything at every tick. As a
/// safety net a pushed status older than the given age is read again, as the system does not announce every percent of
/// charge; and until the first push, and for good if none ever comes, every question is answered by a reading.
/// </summary>
/// <param name="reader">Reads the real power status; a single cheap call.</param>
/// <param name="timeProvider">The time source.</param>
/// <param name="maxAge">How long a pushed status is trusted without a push or a new reading.</param>
public sealed class PushedPowerStatus(IPowerStatusProvider reader, TimeProvider timeProvider, TimeSpan maxAge)
    : IPowerStatusProvider
{
    /// <summary>
    /// Guards the fields below, which the thread that pushes and the thread that asks both touch.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// The latest status, or <see langword="null"/> if nothing was pushed yet.
    /// </summary>
    private PowerStatus? _status;

    /// <summary>
    /// When <see cref="_status"/> was pushed or last read.
    /// </summary>
    private DateTimeOffset _knownAt;

    /// <summary>
    /// Counts the pushes, so that a reading that was under way while one arrived can tell that it is the older of the two.
    /// </summary>
    private long _pushes;

    /// <inheritdoc />
    public PowerStatus GetStatus()
    {
        long pushesBefore;
        lock (_gate)
        {
            if (_status is { } status && timeProvider.GetUtcNow() - _knownAt < maxAge)
            {
                return status;
            }

            pushesBefore = _pushes;
        }

        var read = reader.GetStatus();
        lock (_gate)
        {
            // Something was pushed while reading: the push is newer than the reading, whatever order they finished in.
            if (_pushes != pushesBefore && _status is { } pushed)
            {
                return pushed;
            }

            if (_status is not null)
            {
                _status = read;
                _knownAt = timeProvider.GetUtcNow();
            }
        }

        return read;
    }

    /// <summary>
    /// Tells how the computer is powered now. May be called from any thread.
    /// </summary>
    /// <param name="status">The new status.</param>
    public void Push(PowerStatus status)
    {
        lock (_gate)
        {
            _status = status;
            _knownAt = timeProvider.GetUtcNow();
            _pushes++;
        }
    }
}
