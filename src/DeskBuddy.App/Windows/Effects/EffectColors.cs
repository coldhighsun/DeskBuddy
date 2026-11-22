using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeskBuddy.App.Windows;

/// <summary>
/// The colors of the beams of light of the switch animations: a rainbow that keeps changing, so a beam is not one flat color.
/// </summary>
internal static class EffectColors
{
    /// <summary>
    /// The colors of the rainbow in order, each shading into the next and the last into the first.
    /// </summary>
    private static readonly (byte R, byte G, byte B)[] Rainbow =
    [
        (0xFF, 0x00, 0x00),
        (0xFF, 0xFF, 0x00),
        (0x00, 0xFF, 0x00),
        (0x00, 0xFF, 0xFF),
        (0x00, 0x00, 0xFF),
        (0xFF, 0x00, 0xFF),
    ];

    /// <summary>
    /// How many key frames a full turn of the rainbow is animated with; enough that the shading between two of them looks
    /// like a smooth turn of the hue wherever the turn starts.
    /// </summary>
    private const int KeyFramesPerTurn = 12;

    /// <summary>
    /// Works out the color at a point of the rainbow.
    /// </summary>
    /// <param name="phase">
    /// Where in the rainbow: 0 is red, a sixth is yellow, and so on; 1 is red again. Any number is allowed, and it wraps.
    /// </param>
    /// <param name="alpha">How opaque the color is.</param>
    /// <param name="whiten">How much white is mixed in, from 0 (the pure color) to 1 (white), for a paler color.</param>
    /// <returns>The color.</returns>
    public static Color At(double phase, byte alpha, double whiten = 0)
    {
        var position = (phase - Math.Floor(phase)) * Rainbow.Length;
        var index = (int)Math.Floor(position) % Rainbow.Length;
        var fraction = position - Math.Floor(position);
        var from = Rainbow[index];
        var to = Rainbow[(index + 1) % Rainbow.Length];
        return Color.FromArgb(
            alpha,
            Mix(Mix(from.R, to.R, fraction), 0xFF, whiten),
            Mix(Mix(from.G, to.G, fraction), 0xFF, whiten),
            Mix(Mix(from.B, to.B, fraction), 0xFF, whiten));
    }

    /// <summary>
    /// Makes a gradient whose colors run round the rainbow for as long as it is shown. Each stop of the gradient has its
    /// own point of the rainbow to start from, so the colors seem to flow along the gradient.
    /// </summary>
    /// <param name="start">Where the gradient starts, as fractions of the shape it fills.</param>
    /// <param name="end">Where the gradient ends, as fractions of the shape it fills.</param>
    /// <param name="stops">
    /// The stops: where each is along the gradient (0 to 1), how opaque it is, and the point of the rainbow it starts from.
    /// </param>
    /// <param name="period">How long a full turn of the rainbow takes.</param>
    /// <returns>The gradient brush.</returns>
    public static LinearGradientBrush Gradient(Point start, Point end, (double Offset, byte Alpha, double Phase)[] stops, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(stops);

        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var (offset, alpha, phase) in stops)
        {
            var stop = new GradientStop(At(phase, alpha), offset);
            Cycle(stop, alpha, period, phase);
            brush.GradientStops.Add(stop);
        }

        return brush;
    }

    /// <summary>
    /// Makes a round gradient, light at the middle and fading out at the rim, whose colors run round the rainbow for as long
    /// as it is shown; the colors of a puff of smoke.
    /// </summary>
    /// <param name="phase">The point of the rainbow the middle starts from; the rim is a little way on from it.</param>
    /// <param name="period">How long a full turn of the rainbow takes.</param>
    /// <param name="whiten">How much white is mixed in, for paler colors.</param>
    /// <returns>The gradient brush.</returns>
    public static RadialGradientBrush Puff(double phase, TimeSpan period, double whiten)
    {
        var brush = new RadialGradientBrush();
        foreach (var (offset, alpha, shift) in new (double, byte, double)[] { (0, 0xFF, 0), (1, 0xE8, 0.12) })
        {
            var stop = new GradientStop(At(phase + shift, alpha, whiten), offset);
            Cycle(stop, alpha, period, phase + shift, whiten);
            brush.GradientStops.Add(stop);
        }

        return brush;
    }

    /// <summary>
    /// Makes the color of a gradient stop run round the rainbow, over and over.
    /// </summary>
    /// <param name="stop">The gradient stop.</param>
    /// <param name="alpha">How opaque the color is; it does not change.</param>
    /// <param name="period">How long a full turn of the rainbow takes.</param>
    /// <param name="phase">The point of the rainbow to start from.</param>
    /// <param name="whiten">How much white is mixed in, for paler colors.</param>
    private static void Cycle(GradientStop stop, byte alpha, TimeSpan period, double phase, double whiten = 0)
    {
        var frames = new ColorAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        for (var i = 0; i <= KeyFramesPerTurn; i++)
        {
            var fraction = (double)i / KeyFramesPerTurn;
            frames.KeyFrames.Add(new LinearColorKeyFrame(At(phase + fraction, alpha, whiten), KeyTime.FromTimeSpan(period * fraction)));
        }

        stop.BeginAnimation(GradientStop.ColorProperty, frames);
    }

    /// <summary>
    /// Mixes two color parts.
    /// </summary>
    /// <param name="from">The part at the start.</param>
    /// <param name="to">The part at the end.</param>
    /// <param name="fraction">How far from the start to the end, from 0 to 1.</param>
    /// <returns>The mixed part.</returns>
    private static byte Mix(double from, double to, double fraction)
    {
        return (byte)Math.Round(from + ((to - from) * fraction));
    }
}
