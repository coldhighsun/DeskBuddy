namespace DeskBuddy.Domain.Pets;

/// <summary>
/// A point on a pet's frame as a fraction of the frame's width and height, so it holds at every size the pet is shown
/// at: (0, 0) is the top left corner of the frame, (1, 1) the bottom right one.
/// </summary>
/// <param name="X">
/// Distance from the left edge as a fraction of the width, from 0 to 1.
/// </param>
/// <param name="Y">
/// Distance from the top edge as a fraction of the height, from 0 to 1.
/// </param>
public readonly record struct FramePoint(double X, double Y)
{
    /// <summary>
    /// Gets the same point on the frame drawn mirrored left to right, as the window shows the pet facing the other way.
    /// </summary>
    public FramePoint Mirrored => new(1 - X, Y);

    /// <summary>
    /// Gets a value indicating whether the point is on the frame, its edges included.
    /// </summary>
    public bool IsOnFrame => X is >= 0 and <= 1 && Y is >= 0 and <= 1;
}
