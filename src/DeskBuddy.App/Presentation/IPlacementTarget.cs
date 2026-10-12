namespace DeskBuddy.App.Presentation;

/// <summary>
/// A window that a pet can be placed on: what <see cref="PetPlacement.Place"/> needs to know about it and to ask of it.
/// </summary>
public interface IPlacementTarget
{
    /// <summary>
    /// Gets a value indicating whether the window is shown. A shown window follows moves made in device pixels; a window
    /// that is not shown (it does not exist yet, or is hidden) is placed through <see cref="Left"/> and <see cref="Top"/>.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Gets the horizontal scaling of the window now: device pixels per device-independent pixel. It changes when the
    /// window moves to a monitor of another scaling, so it is read again whenever it is needed.
    /// </summary>
    double ScaleX { get; }

    /// <summary>
    /// Gets the vertical scaling of the window now; see <see cref="ScaleX"/>.
    /// </summary>
    double ScaleY { get; }

    /// <summary>
    /// Gets or sets the left edge of the window in device-independent pixels.
    /// </summary>
    double Left { get; set; }

    /// <summary>
    /// Gets or sets the top edge of the window in device-independent pixels.
    /// </summary>
    double Top { get; set; }

    /// <summary>
    /// Moves the window in one step to a position in device pixels of the virtual screen, so that the scaling cannot change
    /// between the two coordinates.
    /// </summary>
    /// <param name="x">
    /// The left edge in device pixels.
    /// </param>
    /// <param name="y">
    /// The top edge in device pixels.
    /// </param>
    void MoveToPixels(int x, int y);
}
