namespace DeskBuddy.Domain.Movement;

/// <summary>
/// A point or a size on the desktop, in device-independent pixels.
/// </summary>
/// <param name="X">
/// Horizontal coordinate, or width.
/// </param>
/// <param name="Y">
/// Vertical coordinate, or height.
/// </param>
public readonly record struct Point2(double X, double Y);

/// <summary>
/// The range the pet window's top-left corner may take. When the range is narrower than a point (the area is
/// smaller than the window) the minimum wins.
/// </summary>
/// <param name="MinX">
/// Smallest allowed horizontal position.
/// </param>
/// <param name="MaxX">
/// Largest allowed horizontal position.
/// </param>
/// <param name="MinY">
/// Smallest allowed vertical position.
/// </param>
/// <param name="MaxY">
/// Largest allowed vertical position; the taskbar line.
/// </param>
public readonly record struct MovementBounds(double MinX, double MaxX, double MinY, double MaxY)
{
    /// <summary>
    /// Moves a position into the range.
    /// </summary>
    /// <param name="position">
    /// The position to clamp.
    /// </param>
    /// <returns>
    /// The nearest position inside the range.
    /// </returns>
    public Point2 Clamp(Point2 position)
    {
        return new Point2(
            Math.Clamp(position.X, MinX, Math.Max(MinX, MaxX)),
            Math.Clamp(position.Y, MinY, Math.Max(MinY, MaxY)));
    }
}

/// <summary>
/// What a movement strategy needs to know to move the pet one step.
/// </summary>
/// <param name="Position">
/// The window's top-left corner.
/// </param>
/// <param name="Size">
/// The window's size.
/// </param>
/// <param name="Bounds">
/// The range the window's top-left corner may take.
/// </param>
/// <param name="Speed">
/// The walking speed in device-independent pixels per second.
/// </param>
public readonly record struct MovementContext(Point2 Position, Point2 Size, MovementBounds Bounds, double Speed);

/// <summary>
/// The result of one movement step.
/// </summary>
/// <param name="Position">
/// The new position of the window's top-left corner.
/// </param>
/// <param name="Facing">
/// Which way the sprite faces: <c>-1</c> for left, <c>1</c> for right.
/// </param>
public readonly record struct MovementStep(Point2 Position, int Facing);
