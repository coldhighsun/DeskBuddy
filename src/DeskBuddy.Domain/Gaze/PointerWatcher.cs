namespace DeskBuddy.Domain.Gaze;

/// <summary>
/// Where the pet is on the screen, for working out where to look.
/// </summary>
/// <param name="CenterX">
/// The horizontal centre, in pixels.
/// </param>
/// <param name="CenterY">
/// The vertical centre, in pixels.
/// </param>
/// <param name="Size">
/// The width, in pixels.
/// </param>
public readonly record struct PetBox(double CenterX, double CenterY, double Size);

/// <summary>
/// What the pet should do about the mouse pointer.
/// </summary>
/// <param name="BecomeDizzy">
/// Whether the mouse was just shaken, so that the pet gets dizzy.
/// </param>
/// <param name="Gaze">
/// The direction to look in, or <see langword="null"/> if the eyes need no new instruction this time.
/// </param>
public readonly record struct PointerReaction(bool BecomeDizzy, GazeDirection? Gaze);

/// <summary>
/// Looks at the mouse pointer every so often and decides whether the pet gets dizzy from a shake or turns its eyes.
/// </summary>
/// <param name="shake">
/// Notices the shaking.
/// </param>
public sealed class PointerWatcher(MouseShakeDetector shake)
{
    /// <summary>
    /// How many readings pass between two turns of the eyes.
    /// </summary>
    public const int GazeEveryReadings = 4;

    /// <summary>
    /// The shortest time between two bouts of dizziness.
    /// </summary>
    public static readonly TimeSpan DizzyCooldown = TimeSpan.FromSeconds(6);

    /// <summary>
    /// When the pet was last dizzy, on the clock the readings come with.
    /// </summary>
    private TimeSpan? _lastDizzy;

    /// <summary>
    /// Readings since the start.
    /// </summary>
    private int _readings;

    /// <summary>
    /// Takes in a reading of the pointer.
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
    /// <param name="dizzyAllowed">
    /// Whether a shake may make the pet dizzy now.
    /// </param>
    /// <param name="canLook">
    /// Whether the pet can turn its eyes now: it stands around and its skin has looks.
    /// </param>
    /// <param name="petBox">
    /// Tells where the pet is; only asked for when the eyes are about to turn.
    /// </param>
    /// <returns>
    /// What to do about it.
    /// </returns>
    public PointerReaction Read(TimeSpan now, int x, int y, bool dizzyAllowed, bool canLook, Func<PetBox> petBox)
    {
        ArgumentNullException.ThrowIfNull(petBox);

        if (dizzyAllowed && shake.Update(now, x, y))
        {
            if (_lastDizzy is { } last && now - last < DizzyCooldown)
            {
                return default;
            }

            _lastDizzy = now;
            return new PointerReaction(true, null);
        }

        if (++_readings % GazeEveryReadings != 0)
        {
            return default;
        }

        if (!canLook)
        {
            return new PointerReaction(false, GazeDirection.None);
        }

        var box = petBox();
        return new PointerReaction(false, GazeTracker.Of(x - box.CenterX, y - box.CenterY, box.Size / 2));
    }
}
