namespace DeskBuddy.App.Presentation;

/// <summary>
/// Where the umbrella or parachute window goes, and how big it is, for a pet window at a given place. Worked out from
/// the pet's pixel bounds alone, so the pet and the canopy can be moved together to their new places in one step.
/// </summary>
/// <param name="Width">
/// Width of the canopy window in device-independent pixels.
/// </param>
/// <param name="Height">
/// Height of the canopy window in device-independent pixels.
/// </param>
/// <param name="X">
/// Left edge of the canopy window in device pixels.
/// </param>
/// <param name="Y">
/// Top edge of the canopy window in device pixels.
/// </param>
public readonly record struct CanopyPlacement(double Width, double Height, int X, int Y)
{
    /// <summary>
    /// Width of the canopy relative to the width of the pet.
    /// </summary>
    private const double WidthRatio = 1.5;

    /// <summary>
    /// Smallest width of the window in device-independent pixels, so a small pet still gets a visible canopy.
    /// </summary>
    private const double MinCanopyWidth = 90;

    /// <summary>
    /// Height of the window relative to its width, matching the 140 x 112 drawing.
    /// </summary>
    private const double AspectRatio = 112.0 / 140.0;

    /// <summary>
    /// How far down the pet window, relative to its height, the bottom of the handle or strings reaches, so they meet
    /// the pet's head rather than the empty space above it.
    /// </summary>
    private const double HeadOverlapRatio = 0.12;

    /// <summary>
    /// Works out the canopy window for a pet window: centered over it, with the bottom of the handle at its head.
    /// </summary>
    /// <param name="petBounds">
    /// Where the pet window is, or is about to be, in device pixels of the virtual screen.
    /// </param>
    /// <param name="petWidth">
    /// Width of the pet window in device-independent pixels.
    /// </param>
    /// <param name="petScale">
    /// Device pixels per device-independent pixel on the pet's monitor.
    /// </param>
    /// <returns>
    /// The size and position of the canopy window.
    /// </returns>
    public static CanopyPlacement For(PixelRect petBounds, double petWidth, double petScale)
    {
        var width = Math.Max(MinCanopyWidth, petWidth * WidthRatio);
        var height = width * AspectRatio;
        var widthPixels = (int)Math.Round(width * petScale, MidpointRounding.AwayFromZero);
        var heightPixels = (int)Math.Round(height * petScale, MidpointRounding.AwayFromZero);
        var x = petBounds.Left + ((petBounds.Width - widthPixels) / 2);
        var y = petBounds.Top + (int)Math.Round(petBounds.Height * HeadOverlapRatio, MidpointRounding.AwayFromZero) - heightPixels;
        return new CanopyPlacement(width, height, x, y);
    }
}
