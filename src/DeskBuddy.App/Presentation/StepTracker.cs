using DeskBuddy.Domain.Movement;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Keeps track of where the pet window is for the walk and crawl steps. The window manager works in device pixels, the
/// movement in device-independent pixels of the window's present scaling, and <see cref="System.Windows.Window.Left"/>
/// is in the scaling the window had when it was last moved, so after the pet has walked onto a monitor with another
/// scaling it can be out by the ratio of the two scalings. The position is therefore always derived from the real
/// pixels. The exact position of the last step is kept only to not lose steps of less than a pixel, and is dropped as
/// soon as the window is not where that step put it, or the scaling is not what it was.
/// </summary>
public sealed class StepTracker
{
    /// <summary>
    /// The device pixels the window was put at by the last step, or <see langword="null"/> if none is remembered.
    /// </summary>
    private (int X, int Y)? _pixels;

    /// <summary>
    /// The exact position, in device-independent pixels, that the last step worked out.
    /// </summary>
    private Point2 _position;

    /// <summary>
    /// The horizontal scaling that applied to <see cref="_position"/>.
    /// </summary>
    private double _scaleX;

    /// <summary>
    /// The vertical scaling that applied to <see cref="_position"/>.
    /// </summary>
    private double _scaleY;

    /// <summary>
    /// Works out the device pixels a position is to be moved to.
    /// </summary>
    /// <param name="position">
    /// The wanted position of the window's top-left corner, in device-independent pixels.
    /// </param>
    /// <param name="scaleX">
    /// Device pixels per device-independent pixel, horizontally.
    /// </param>
    /// <param name="scaleY">
    /// Device pixels per device-independent pixel, vertically.
    /// </param>
    /// <returns>
    /// The nearest whole device pixels.
    /// </returns>
    public static (int X, int Y) ToPixels(Point2 position, double scaleX, double scaleY)
    {
        return (
            (int)Math.Round(position.X * scaleX, MidpointRounding.AwayFromZero),
            (int)Math.Round(position.Y * scaleY, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Tells where the window is, in device-independent pixels of its present scaling.
    /// </summary>
    /// <param name="actual">
    /// The real bounds of the window, in device pixels.
    /// </param>
    /// <param name="scaleX">
    /// Device pixels per device-independent pixel now, horizontally.
    /// </param>
    /// <param name="scaleY">
    /// Device pixels per device-independent pixel now, vertically.
    /// </param>
    /// <returns>
    /// The position the last step worked out, if the window is still at the pixels that step put it at and the scaling
    /// is the same; otherwise the real pixels divided by the scaling.
    /// </returns>
    public Point2 Resolve(PixelRect actual, double scaleX, double scaleY)
    {
        if (_pixels is { } last && last.X == actual.Left && last.Y == actual.Top && _scaleX == scaleX && _scaleY == scaleY)
        {
            return _position;
        }

        return new Point2(actual.Left / scaleX, actual.Top / scaleY);
    }

    /// <summary>
    /// Remembers where a step put the window.
    /// </summary>
    /// <param name="pixels">
    /// The device pixels the window was put at.
    /// </param>
    /// <param name="position">
    /// The exact position of the step, in device-independent pixels of the given scaling.
    /// </param>
    /// <param name="scaleX">
    /// Device pixels per device-independent pixel, horizontally.
    /// </param>
    /// <param name="scaleY">
    /// Device pixels per device-independent pixel, vertically.
    /// </param>
    public void Remember((int X, int Y) pixels, Point2 position, double scaleX, double scaleY)
    {
        _pixels = pixels;
        _position = position;
        _scaleX = scaleX;
        _scaleY = scaleY;
    }
}
