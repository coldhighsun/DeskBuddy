namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Works out how fast a dragged pet was moving when it was let go, from where the pointer was over the last moments of
/// the drag, so a pet can be thrown. A drag that ends slowly, or that stopped before the release, throws nothing.
/// </summary>
/// <param name="options">
/// The throw tuning.
/// </param>
public sealed class ThrowTracker(PetBehaviorOptions options)
{
    /// <summary>
    /// The pointer positions of the last moments, oldest first.
    /// </summary>
    private readonly Queue<(TimeSpan Time, double X, double Y)> _samples = new();

    /// <summary>
    /// Forgets the positions, for the start of a new drag.
    /// </summary>
    public void Reset()
    {
        _samples.Clear();
    }

    /// <summary>
    /// Takes in where the pointer is during the drag.
    /// </summary>
    /// <param name="time">
    /// The time of the reading, from any clock that only moves forward.
    /// </param>
    /// <param name="x">
    /// The horizontal position, in device-independent pixels.
    /// </param>
    /// <param name="y">
    /// The vertical position, in device-independent pixels.
    /// </param>
    public void Add(TimeSpan time, double x, double y)
    {
        _samples.Enqueue((time, x, y));
        while (_samples.Count > 2 && time - _samples.Peek().Time > options.ThrowSampleWindow)
        {
            _samples.Dequeue();
        }
    }

    /// <summary>
    /// Gets the speed to throw the pet with when it is let go now.
    /// </summary>
    /// <param name="now">
    /// The time of the release, on the clock of <see cref="Add"/>.
    /// </param>
    /// <returns>
    /// The horizontal and vertical speed in device-independent pixels per second (positive is right and down), limited
    /// to <see cref="PetBehaviorOptions.ThrowMaxSpeed"/>; zero if the pointer moved slower than
    /// <see cref="PetBehaviorOptions.ThrowMinSpeed"/> or had come to rest before the release.
    /// </returns>
    public (double X, double Y) GetVelocity(TimeSpan now)
    {
        if (_samples.Count < 2)
        {
            return default;
        }

        var last = _samples.Last();

        // A pointer held still for a moment before letting go means a careful drop, not a throw.
        if (now - last.Time > options.ThrowSampleWindow)
        {
            return default;
        }

        var first = _samples.Peek();
        var seconds = (last.Time - first.Time).TotalSeconds;
        if (seconds <= 0)
        {
            return default;
        }

        var vx = (last.X - first.X) / seconds;
        var vy = (last.Y - first.Y) / seconds;
        var speed = Math.Sqrt((vx * vx) + (vy * vy));
        if (speed < options.ThrowMinSpeed)
        {
            return default;
        }

        if (speed > options.ThrowMaxSpeed)
        {
            var scale = options.ThrowMaxSpeed / speed;
            vx *= scale;
            vy *= scale;
        }

        return (vx, vy);
    }
}
