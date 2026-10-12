namespace DeskBuddy.Domain.Movement;

/// <summary>
/// Decides where the pet walks. One strategy exists per disturbance mode: walking along the taskbar, or roaming the
/// desktop.
/// </summary>
public interface IMovementStrategy
{
    /// <summary>
    /// Gets a value indicating whether a pet dropped high above its resting place falls down to it. A mode in which the
    /// pet rests wherever it is put has no resting place to fall to.
    /// </summary>
    bool FallsWhenDropped { get; }

    /// <summary>
    /// Prepares for a new walk.
    /// </summary>
    /// <param name="context">
    /// Where the pet is and where it may go.
    /// </param>
    /// <param name="preferredDirection">
    /// The horizontal direction the behavior state machine picked for this walk: <c>-1</c> or <c>1</c>.
    /// </param>
    /// <returns>
    /// Which way the sprite faces at the start of the walk: <c>-1</c> for left, <c>1</c> for right.
    /// </returns>
    int BeginWalk(MovementContext context, int preferredDirection);

    /// <summary>
    /// Moves the pet one step.
    /// </summary>
    /// <param name="context">
    /// Where the pet is and where it may go.
    /// </param>
    /// <param name="delta">
    /// Time elapsed since the previous step.
    /// </param>
    /// <returns>
    /// The new position and facing.
    /// </returns>
    MovementStep Step(MovementContext context, TimeSpan delta);

    /// <summary>
    /// Finds where the pet rests after being dropped or when the mode changes.
    /// </summary>
    /// <param name="position">
    /// Where the pet is now.
    /// </param>
    /// <param name="bounds">
    /// The range the pet may take.
    /// </param>
    /// <returns>
    /// The position to put the pet at.
    /// </returns>
    Point2 Settle(Point2 position, MovementBounds bounds);
}
