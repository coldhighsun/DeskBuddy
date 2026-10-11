namespace DeskBuddy.Domain.Gaze;

/// <summary>
/// Notices the mouse being shaken: quick, wide swings back and forth, sideways or up and down.
/// </summary>
/// <param name="window">
/// The time within which the swings must happen.
/// </param>
/// <param name="reversalsNeeded">
/// How many changes of direction along one axis make a shake.
/// </param>
/// <param name="minSwing">
/// How far the pointer must travel back, in pixels, for a change of direction to count; smaller wobbles are ignored.
/// </param>
public sealed class MouseShakeDetector(TimeSpan window, int reversalsNeeded, int minSwing)
{
    /// <summary>
    /// Follows the horizontal movement.
    /// </summary>
    private readonly Axis _horizontal = new(window, minSwing);

    /// <summary>
    /// Follows the vertical movement.
    /// </summary>
    private readonly Axis _vertical = new(window, minSwing);

    /// <summary>
    /// Initializes a new instance of the <see cref="MouseShakeDetector"/> class with the usual values: four changes of
    /// direction of at least 80 pixels within 1.2 seconds.
    /// </summary>
    public MouseShakeDetector()
        : this(TimeSpan.FromSeconds(1.2), 4, 80)
    {
    }

    /// <summary>
    /// Takes in the pointer position.
    /// </summary>
    /// <param name="now">
    /// The time of the reading, from any clock that only moves forward.
    /// </param>
    /// <param name="x">
    /// The horizontal position of the pointer, in pixels.
    /// </param>
    /// <param name="y">
    /// The vertical position of the pointer, in pixels.
    /// </param>
    /// <returns>
    /// <see langword="true"/> once the mouse has been shaken; the detector then starts over.
    /// </returns>
    public bool Update(TimeSpan now, int x, int y = 0)
    {
        var horizontal = _horizontal.Update(now, x);
        var vertical = _vertical.Update(now, y);
        if (horizontal < reversalsNeeded && vertical < reversalsNeeded)
        {
            return false;
        }

        Reset(x, y);
        return true;
    }

    /// <summary>
    /// Forgets what was seen so far, for example after the pet reacted.
    /// </summary>
    /// <param name="x">
    /// The current horizontal position of the pointer.
    /// </param>
    /// <param name="y">
    /// The current vertical position of the pointer.
    /// </param>
    public void Reset(int x, int y = 0)
    {
        _horizontal.Reset(x);
        _vertical.Reset(y);
    }

    /// <summary>
    /// Counts the changes of direction along one axis.
    /// </summary>
    /// <param name="window">
    /// The time within which changes of direction are counted.
    /// </param>
    /// <param name="minSwing">
    /// How far the pointer must travel back for a change to count.
    /// </param>
    private sealed class Axis(TimeSpan window, int minSwing)
    {
        /// <summary>
        /// When the direction changed lately.
        /// </summary>
        private readonly Queue<TimeSpan> _reversals = new();

        /// <summary>
        /// Whether a first position was seen.
        /// </summary>
        private bool _started;

        /// <summary>
        /// The direction of travel, <c>1</c> for increasing and <c>-1</c> for decreasing values, or <c>0</c> while it
        /// is not clear yet.
        /// </summary>
        private int _direction;

        /// <summary>
        /// The furthest point reached in the current direction, or the starting point while the direction is not
        /// clear.
        /// </summary>
        private int _extreme;

        /// <summary>
        /// Takes in a position.
        /// </summary>
        /// <param name="now">
        /// The time of the reading.
        /// </param>
        /// <param name="value">
        /// The position along this axis.
        /// </param>
        /// <returns>
        /// How many changes of direction happened within the window.
        /// </returns>
        public int Update(TimeSpan now, int value)
        {
            if (!_started)
            {
                _started = true;
                _extreme = value;
                return 0;
            }

            if (_direction == 0)
            {
                if (Math.Abs(value - _extreme) >= minSwing)
                {
                    _direction = Math.Sign(value - _extreme);
                    _extreme = value;
                }
            }
            else if ((value - _extreme) * _direction > 0)
            {
                _extreme = value;
            }
            else if ((_extreme - value) * _direction >= minSwing)
            {
                _direction = -_direction;
                _extreme = value;
                _reversals.Enqueue(now);
            }

            while (_reversals.Count > 0 && now - _reversals.Peek() > window)
            {
                _reversals.Dequeue();
            }

            return _reversals.Count;
        }

        /// <summary>
        /// Starts over from a position.
        /// </summary>
        /// <param name="value">
        /// The current position along this axis.
        /// </param>
        public void Reset(int value)
        {
            _reversals.Clear();
            _direction = 0;
            _extreme = value;
        }
    }
}
