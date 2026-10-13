namespace DeskBuddy.App.Presentation;

/// <summary>
/// An axis-aligned rectangle in device pixels of the virtual screen.
/// </summary>
/// <param name="Left">
/// Left edge.
/// </param>
/// <param name="Top">
/// Top edge.
/// </param>
/// <param name="Right">
/// Right edge.
/// </param>
/// <param name="Bottom">
/// Bottom edge.
/// </param>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    /// <summary>
    /// Gets the width.
    /// </summary>
    public int Width => Right - Left;

    /// <summary>
    /// Gets the height.
    /// </summary>
    public int Height => Bottom - Top;
}

/// <summary>
/// Keeps a dragged window under the cursor in device pixels. Device-independent pixels are not usable across monitors
/// with different scaling: the same number means different distances on each, and the window's own scale changes while
/// it is half way across. Pixels of the virtual screen are the same everywhere, and the grab point is remembered as a
/// fraction of the window so it still sits under the cursor when the window is resized to the new monitor's scale.
/// </summary>
public sealed class DragTracker
{
    /// <summary>
    /// Where the cursor grabbed the window, as a fraction of its width.
    /// </summary>
    private double _grabX;

    /// <summary>
    /// Where the cursor grabbed the window, as a fraction of its height.
    /// </summary>
    private double _grabY;

    /// <summary>
    /// Starts a drag.
    /// </summary>
    /// <param name="cursorX">
    /// Cursor X in device pixels.
    /// </param>
    /// <param name="cursorY">
    /// Cursor Y in device pixels.
    /// </param>
    /// <param name="window">
    /// The window's bounds in device pixels.
    /// </param>
    public void Begin(int cursorX, int cursorY, PixelRect window)
    {
        _grabX = window.Width > 0 ? (cursorX - window.Left) / (double)window.Width : 0;
        _grabY = window.Height > 0 ? (cursorY - window.Top) / (double)window.Height : 0;
    }

    /// <summary>
    /// Works out where the window's top-left corner has to be so the grab point is under the cursor.
    /// </summary>
    /// <param name="cursorX">
    /// Cursor X in device pixels.
    /// </param>
    /// <param name="cursorY">
    /// Cursor Y in device pixels.
    /// </param>
    /// <param name="width">
    /// The window's current width in device pixels; it changes when the window crosses to a monitor with another scale.
    /// </param>
    /// <param name="height">
    /// The window's current height in device pixels.
    /// </param>
    /// <returns>
    /// The wanted top-left corner in device pixels.
    /// </returns>
    public (int X, int Y) Move(int cursorX, int cursorY, int width, int height)
    {
        return (
            cursorX - (int)Math.Round(_grabX * width, MidpointRounding.AwayFromZero),
            cursorY - (int)Math.Round(_grabY * height, MidpointRounding.AwayFromZero));
    }
}

/// <summary>
/// Works out where the speech bubble goes, in device pixels of the monitor the pet is on.
/// </summary>
public static class BubblePlacement
{
    /// <summary>
    /// Works out how far the bubble's bottom reaches below the top of the pet window so that it ends just above the
    /// sprite, whatever transparent space the pet leaves above its head.
    /// </summary>
    /// <param name="petHeight">
    /// The pet window's height in device pixels.
    /// </param>
    /// <param name="spriteTop">
    /// Where the sprite starts, as a fraction of the window height.
    /// </param>
    /// <param name="gap">
    /// The space to leave between the bubble and the sprite, in device pixels.
    /// </param>
    /// <returns>
    /// The overlap in device pixels; negative when the bubble has to stay clear of the window.
    /// </returns>
    public static int TailOverlap(int petHeight, double spriteTop, int gap)
    {
        return (int)Math.Round(petHeight * spriteTop, MidpointRounding.AwayFromZero) - gap;
    }

    /// <summary>
    /// Places the bubble above the pet, centred on it but kept inside the work area.
    /// </summary>
    /// <param name="pet">
    /// The pet window's bounds.
    /// </param>
    /// <param name="bubbleWidth">
    /// The bubble's width in device pixels.
    /// </param>
    /// <param name="bubbleHeight">
    /// The bubble's height in device pixels.
    /// </param>
    /// <param name="tailOverlap">
    /// How far the bubble's bottom reaches below the top of the pet window, in device pixels.
    /// </param>
    /// <param name="workArea">
    /// The work area of the pet's monitor.
    /// </param>
    /// <returns>
    /// The bubble's top-left corner, and the pet's horizontal centre, all in device pixels.
    /// </returns>
    public static (int Left, int Top, double PetCenterX) Compute(
        PixelRect pet,
        int bubbleWidth,
        int bubbleHeight,
        int tailOverlap,
        PixelRect workArea)
    {
        var petCenter = pet.Left + (pet.Width / 2.0);
        var left = (int)Math.Round(petCenter - (bubbleWidth / 2.0), MidpointRounding.AwayFromZero);
        left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - bubbleWidth));
        var top = Math.Max(workArea.Top, pet.Top + tailOverlap - bubbleHeight);
        return (left, top, petCenter);
    }
}
