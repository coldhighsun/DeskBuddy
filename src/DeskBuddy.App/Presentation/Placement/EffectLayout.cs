namespace DeskBuddy.App.Presentation;

/// <summary>
/// Where the window of a pet switch animation goes and where the pet is in it. The window is a column on the pet: as wide
/// as the animation needs around the pet, and reaching from the top of the work area (where flying things come from) down
/// to a little below the pet's feet, where things that hold the pet from underneath are drawn. Everything is derived from
/// the pet window's own size, so it fits a pet of any shape and size.
/// </summary>
/// <param name="X">The left edge of the window in device pixels.</param>
/// <param name="Y">The top edge of the window in device pixels.</param>
/// <param name="WidthPixels">The width of the window in device pixels.</param>
/// <param name="HeightPixels">The height of the window in device pixels.</param>
/// <param name="Scale">Device pixels per device-independent pixel.</param>
/// <param name="PetLeft">The left edge of the pet in the window, in device-independent pixels.</param>
/// <param name="PetTop">The top edge of the pet in the window, in device-independent pixels.</param>
/// <param name="PetWidth">The width of the pet, in device-independent pixels.</param>
/// <param name="PetHeight">The height of the pet, in device-independent pixels.</param>
internal sealed record EffectLayout(
    int X,
    int Y,
    int WidthPixels,
    int HeightPixels,
    double Scale,
    double PetLeft,
    double PetTop,
    double PetWidth,
    double PetHeight)
{
    /// <summary>
    /// How many times the larger side of the pet the window is wide.
    /// </summary>
    private const double WidthFactor = 2.4;

    /// <summary>
    /// How far the window reaches below the pet's feet, as a multiple of the pet's larger side.
    /// </summary>
    private const double GroundPadFactor = 0.75;

    /// <summary>
    /// The smallest a window gets, in device-independent pixels, so that the animation of a very small pet is still drawn
    /// whole.
    /// </summary>
    private const double MinWidth = 120;

    /// <summary>
    /// Gets the width of the window in device-independent pixels.
    /// </summary>
    public double Width => WidthPixels / Scale;

    /// <summary>
    /// Gets the height of the window in device-independent pixels.
    /// </summary>
    public double Height => HeightPixels / Scale;

    /// <summary>
    /// Gets the larger side of the pet, the unit the drawings are sized by.
    /// </summary>
    public double Unit => Math.Max(PetWidth, PetHeight);

    /// <summary>
    /// Gets how far the window reaches below the pet's feet, in device-independent pixels.
    /// </summary>
    public double GroundPad => Height - PetBottom;

    /// <summary>
    /// Gets the horizontal middle of the pet in the window, in device-independent pixels.
    /// </summary>
    public double CenterX => PetLeft + (PetWidth / 2);

    /// <summary>
    /// Gets the bottom of the pet in the window, in device-independent pixels.
    /// </summary>
    public double PetBottom => PetTop + PetHeight;

    /// <summary>
    /// Works out the layout for a pet window.
    /// </summary>
    /// <param name="pet">Where the pet window is, in device pixels of the virtual screen.</param>
    /// <param name="workArea">The work area of the monitor the pet is on, in device pixels.</param>
    /// <param name="scale">Device pixels per device-independent pixel on the pet's monitor.</param>
    /// <returns>The layout; the window is centered on the pet, so the pet is in the middle of it sideways.</returns>
    public static EffectLayout For(PixelRect pet, PixelRect workArea, double scale)
    {
        var petWidth = pet.Width / scale;
        var petHeight = pet.Height / scale;
        var width = Math.Max(MinWidth, WidthFactor * Math.Max(petWidth, petHeight));
        var widthPixels = (int)Math.Ceiling(width * scale);

        var centerX = (pet.Left + pet.Right) / 2;
        var x = centerX - (widthPixels / 2);
        var y = Math.Min(workArea.Top, pet.Top);
        var padPixels = (int)Math.Ceiling(GroundPadFactor * Math.Max(petWidth, petHeight) * scale);
        var heightPixels = Math.Max(1, pet.Bottom - y) + padPixels;

        return new EffectLayout(
            x,
            y,
            widthPixels,
            heightPixels,
            scale,
            (pet.Left - x) / scale,
            (pet.Top - y) / scale,
            petWidth,
            petHeight);
    }
}
