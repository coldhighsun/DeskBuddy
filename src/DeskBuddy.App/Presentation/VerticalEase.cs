namespace DeskBuddy.App.Presentation;

/// <summary>
/// How the height of the pet changes when it walks onto a monitor whose taskbar line or top edge is at another height
/// than that of the monitor it came from.
/// </summary>
public enum VerticalChange
{
    /// <summary>
    /// The height is the same, within the tolerance.
    /// </summary>
    None,

    /// <summary>
    /// The target is higher up on the screen: the pet climbs up to it.
    /// </summary>
    Rise,

    /// <summary>
    /// The target is lower down on the screen: the pet falls to it, or slides down if it is not far.
    /// </summary>
    Drop,
}

/// <summary>
/// Moves the pet to a new height smoothly. The taskbar line and the top edge of monitors side by side need not be at the
/// same height, and the pet would otherwise jump by the difference in a single frame when it gets from one to the other.
/// </summary>
public static class VerticalEase
{
    /// <summary>
    /// Tells in which direction the height has to change.
    /// </summary>
    /// <param name="current">
    /// The height now (screen coordinates: larger is lower).
    /// </param>
    /// <param name="target">
    /// The height to go to.
    /// </param>
    /// <param name="tolerance">
    /// The difference below which there is no change.
    /// </param>
    /// <returns>
    /// <see cref="VerticalChange.None"/> if the heights differ by no more than the tolerance.
    /// </returns>
    public static VerticalChange Classify(double current, double target, double tolerance)
    {
        var difference = target - current;
        if (Math.Abs(difference) <= tolerance)
        {
            return VerticalChange.None;
        }

        return difference > 0 ? VerticalChange.Drop : VerticalChange.Rise;
    }

    /// <summary>
    /// Moves a height towards a target by at most a given distance.
    /// </summary>
    /// <param name="current">
    /// The height now.
    /// </param>
    /// <param name="target">
    /// The height to go to.
    /// </param>
    /// <param name="maxStep">
    /// The furthest the height may move in this step.
    /// </param>
    /// <returns>
    /// The new height: the target if it is within reach, otherwise <paramref name="maxStep"/> closer to it. A step that is
    /// not positive means there is no easing, and gives the target at once.
    /// </returns>
    public static double Approach(double current, double target, double maxStep)
    {
        var distance = target - current;
        if (maxStep <= 0 || Math.Abs(distance) <= maxStep)
        {
            return target;
        }

        return current + (Math.Sign(distance) * maxStep);
    }
}
