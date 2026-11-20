using System.Windows;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// What a falling pet holds over its head.
/// </summary>
public enum UmbrellaKind
{
    /// <summary>
    /// A red umbrella, held by its shaft.
    /// </summary>
    Umbrella,

    /// <summary>
    /// A parachute, held by its strings.
    /// </summary>
    Parachute,
}

/// <summary>
/// What the canopy is and where the pet holds on to it.
/// </summary>
/// <param name="Kind">
/// An umbrella or a parachute.
/// </param>
/// <param name="Grips">
/// Where the pet holds on, as points on its frame as it is shown (already mirrored when the pet faces the other way):
/// one or two of them (see <see cref="Pet.GlideGrips"/>).
/// </param>
public sealed record CanopyRig(UmbrellaKind Kind, IReadOnlyList<FramePoint> Grips)
{
    /// <summary>
    /// Gets an umbrella held where a pet that does not say otherwise holds it.
    /// </summary>
    public static CanopyRig Default { get; } = new(UmbrellaKind.Umbrella, [Pet.DefaultGlideGrip]);

    /// <summary>
    /// Works out the canopy for a pet, from the points its Glide frames hold on at and the way it faces.
    /// </summary>
    /// <param name="kind">
    /// An umbrella or a parachute.
    /// </param>
    /// <param name="grips">
    /// The pet's grips as its frames are drawn.
    /// </param>
    /// <param name="mirrored">
    /// Whether the window shows the frames mirrored left to right.
    /// </param>
    /// <returns>
    /// The rig.
    /// </returns>
    public static CanopyRig For(UmbrellaKind kind, IReadOnlyList<FramePoint> grips, bool mirrored)
    {
        ArgumentNullException.ThrowIfNull(grips);
        return new CanopyRig(kind, mirrored ? [.. grips.Select(grip => grip.Mirrored)] : grips);
    }
}

/// <summary>
/// Where the umbrella or parachute window goes, and how big it is, for a pet window at a given place. Worked out from
/// the pet's pixel bounds alone, so the pet and the canopy can be moved together to their new places in one step.
/// The window holds the canopy at the top, in a box <see cref="DrawingWidth"/> by <see cref="DomeHeight"/> units of the
/// drawing scaled by <see cref="Scale"/>, and reaches down to where the pet holds on, so the shaft or the strings can
/// be drawn down to its paws.
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
/// <param name="Grips">
/// Where the pet holds on, in device-independent pixels of the canopy window.
/// </param>
public readonly record struct CanopyPlacement(double Width, double Height, int X, int Y, IReadOnlyList<Point> Grips)
{
    /// <summary>
    /// Width of the canopy drawing in its own units.
    /// </summary>
    public const double DrawingWidth = 140;

    /// <summary>
    /// Height of the box the dome of the canopy is drawn in, in units of the drawing; its rim is at the bottom of it.
    /// </summary>
    public const double DomeHeight = 64;

    /// <summary>
    /// Width of the canopy relative to the width of the pet.
    /// </summary>
    private const double WidthRatio = 1.5;

    /// <summary>
    /// Smallest width of the window in device-independent pixels, so a small pet still gets a visible canopy.
    /// </summary>
    private const double MinCanopyWidth = 90;

    /// <summary>
    /// How far above the top of the pet window, relative to its height, the bottom of the dome is, so the canopy clears
    /// ears and props.
    /// </summary>
    private const double DomeGapRatio = 0.2;

    /// <summary>
    /// How far, relative to the pet's height, the window reaches below the lowest grip, so the end of the shaft, which
    /// goes on a little past the paw, is not cut off.
    /// </summary>
    private const double BelowGripRatio = 0.06;

    /// <summary>
    /// Gets the device-independent pixels per unit of the canopy drawing.
    /// </summary>
    public double Scale => Width / DrawingWidth;

    /// <summary>
    /// Works out the canopy window for a pet window: the dome over the pet, centered over the point the umbrella is held
    /// at, or the middle of the points the parachute is held at, with the window reaching down to them.
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
    /// <param name="rig">
    /// What the canopy is and where the pet holds on to it.
    /// </param>
    /// <returns>
    /// The size and position of the canopy window.
    /// </returns>
    public static CanopyPlacement For(PixelRect petBounds, double petWidth, double petScale, CanopyRig rig)
    {
        ArgumentNullException.ThrowIfNull(rig);

        var petHeight = petBounds.Height / petScale;
        var grips = rig.Grips.Select(grip => new Point(grip.X * petWidth, grip.Y * petHeight)).ToList();
        var anchor = rig.Kind == UmbrellaKind.Umbrella ? grips.Max(grip => grip.X) : grips.Average(grip => grip.X);

        var width = Math.Max(MinCanopyWidth, petWidth * WidthRatio);
        var dome = DomeHeight * width / DrawingWidth;
        var above = (petHeight * DomeGapRatio) + dome;
        var left = anchor - (width / 2);
        var height = above + grips.Max(grip => grip.Y) + (petHeight * BelowGripRatio);

        var x = petBounds.Left + (int)Math.Round(left * petScale, MidpointRounding.AwayFromZero);
        var y = petBounds.Top - (int)Math.Round(above * petScale, MidpointRounding.AwayFromZero);
        var inWindow = grips.Select(grip => new Point(grip.X - left, grip.Y + above)).ToList();
        return new CanopyPlacement(width, height, x, y, inWindow);
    }
}
