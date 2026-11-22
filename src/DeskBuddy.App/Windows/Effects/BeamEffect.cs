using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DeskBuddy.App.Windows;

/// <summary>
/// A column of light whose colors flow up while the old pet is taken away and down while the new one comes, and which comes down on the pet and a bright line sweeps up through it, taking the pet away as it goes; then the
/// line sweeps down again and the new pet appears behind it.
/// </summary>
internal sealed class BeamEffect : IPetSwitchEffect
{
    /// <summary>
    /// How long the line takes to sweep over the pet, in milliseconds.
    /// </summary>
    private const int SweepMilliseconds = 520;

    /// <summary>
    /// The column of light.
    /// </summary>
    private readonly Rectangle _column = new() { Opacity = 0 };

    /// <summary>
    /// The sweeping line, with its glow.
    /// </summary>
    private readonly Canvas _line = new();

    /// <summary>
    /// Moves the line up and down.
    /// </summary>
    private readonly TranslateTransform _sweep = new();

    /// <summary>
    /// How much of the rainbow lies along the beam, as a fraction of a full turn.
    /// </summary>
    private const double FlowSpan = 0.6;

    /// <summary>
    /// Whether the colors flow up the beam (while the old pet is taken away) or down (while the new pet comes).
    /// </summary>
    private bool _upward = true;

    /// <summary>
    /// The colors of the column, the glow and the core of the line, made again when the new pet comes so that they flow the
    /// other way.
    /// </summary>
    private LinearGradientBrush? _columnFill;

    /// <summary>
    /// The color of the glow round the line; see <see cref="_columnFill"/>.
    /// </summary>
    private LinearGradientBrush? _glowFill;

    /// <summary>
    /// The color of the core of the line; see <see cref="_columnFill"/>.
    /// </summary>
    private LinearGradientBrush? _coreFill;

    /// <inheritdoc />
    public async Task LeaveAsync(EffectStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var layout = stage.Layout;
        _upward = true;
        _columnFill = _glowFill = _coreFill = null;
        Build(stage);
        _sweep.Y = 0;
        var clip = new RectangleGeometry(new Rect(0, 0, layout.PetWidth, layout.PetHeight));
        stage.Pet.Clip = clip;

        await stage.AnimateAsync(_column, UIElement.OpacityProperty, 1, 150);

        // From the feet up to the head, the pet is gone below the line.
        await Task.WhenAll(
            stage.AnimateAsync(_sweep, TranslateTransform.YProperty, -layout.PetHeight, SweepMilliseconds),
            stage.AnimateRectAsync(clip, new Rect(0, 0, layout.PetWidth, layout.PetHeight), new Rect(0, 0, layout.PetWidth, 0), SweepMilliseconds));
        stage.Pet.Opacity = 0;
    }

    /// <inheritdoc />
    public async Task EnterAsync(EffectStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        // The new pet may be of another size, so the column and the line are made again for it, and their colors now flow down.
        var layout = stage.Layout;
        _upward = false;
        _columnFill = _glowFill = _coreFill = null;
        Build(stage);

        // What the first part animated is held where it ended; it has to be let go of before it can be set.
        _column.BeginAnimation(UIElement.OpacityProperty, null);
        _column.Opacity = 1;
        _sweep.BeginAnimation(TranslateTransform.YProperty, null);
        _sweep.Y = -layout.PetHeight;
        var clip = new RectangleGeometry(new Rect(0, 0, layout.PetWidth, 0));
        stage.Pet.Clip = clip;
        stage.Pet.Opacity = 1;

        // From the head down to the feet, the pet shows above the line.
        await Task.WhenAll(
            stage.AnimateAsync(_sweep, TranslateTransform.YProperty, 0, SweepMilliseconds),
            stage.AnimateRectAsync(clip, new Rect(0, 0, layout.PetWidth, 0), new Rect(0, 0, layout.PetWidth, layout.PetHeight), SweepMilliseconds));
        stage.Pet.Clip = null;

        await Task.WhenAll(
            stage.AnimateAsync(_column, UIElement.OpacityProperty, 0, 180),
            stage.AnimateAsync(_line, UIElement.OpacityProperty, 0, 180));
    }

    /// <summary>
    /// Draws the column and the line for the pet in the layout, and puts them on the canvas in place of the last ones.
    /// </summary>
    /// <param name="stage">What they are drawn on.</param>
    private void Build(EffectStage stage)
    {
        var layout = stage.Layout;
        var canvas = stage.Canvas;
        canvas.Children.Remove(_column);
        canvas.Children.Remove(_line);

        var columnWidth = 1.5 * layout.PetWidth;
        _column.Width = columnWidth;
        _column.Height = layout.PetBottom + 4;
        _column.Fill = _columnFill ??= Rainbow(0x70, 0, TimeSpan.FromSeconds(2), _upward);
        _column.OpacityMask = SideFade();
        Canvas.SetLeft(_column, layout.CenterX - (columnWidth / 2));
        Canvas.SetTop(_column, 0);

        var lineWidth = 1.4 * layout.PetWidth;
        var thickness = Math.Max(2, 0.035 * layout.PetHeight);
        _line.Children.Clear();
        _line.Opacity = 1;
        _line.RenderTransform = _sweep;
        _line.Children.Add(Bar(lineWidth, thickness * 5, -thickness * 2.5, _glowFill ??= Rainbow(0x50, 0.5, TimeSpan.FromSeconds(1), _upward)));
        _line.Children.Add(Bar(lineWidth, thickness, 0, _coreFill ??= Rainbow(0xFF, 0.2, TimeSpan.FromSeconds(1), _upward)));
        Canvas.SetLeft(_line, layout.CenterX - (lineWidth / 2));
        Canvas.SetTop(_line, layout.PetBottom);

        canvas.Children.Add(_column);
        canvas.Children.Add(_line);
    }

    /// <summary>
    /// Makes a bar of the sweeping line, with its glow, fading out at both ends.
    /// </summary>
    /// <param name="width">How wide it is.</param>
    /// <param name="height">How high it is.</param>
    /// <param name="top">Its top edge in the line.</param>
    /// <param name="brush">What fills it.</param>
    /// <returns>The bar.</returns>
    private static Rectangle Bar(double width, double height, double top, Brush brush)
    {
        var bar = new Rectangle { Width = width, Height = height, Fill = brush, OpacityMask = SideFade() };
        Canvas.SetTop(bar, top);
        return bar;
    }

    /// <summary>
    /// Makes a brush whose colors run round the rainbow and flow along the beam, up or down.
    /// </summary>
    /// <param name="alpha">How opaque it is.</param>
    /// <param name="phase">The point of the rainbow the top starts from.</param>
    /// <param name="period">How long a full turn of the rainbow takes.</param>
    /// <param name="upward">Whether the colors flow up the beam, or else down.</param>
    /// <returns>The brush.</returns>
    private static LinearGradientBrush Rainbow(byte alpha, double phase, TimeSpan period, bool upward)
    {
        // Every stop's phase keeps growing, so a color moves toward the stops whose phase is lower: up when the phase grows going down.
        var slope = upward ? FlowSpan : -FlowSpan;
        var stops = new (double, byte, double)[5];
        for (var i = 0; i < stops.Length; i++)
        {
            var offset = i / (double)(stops.Length - 1);
            stops[i] = (offset, alpha, phase + (slope * offset));
        }

        return EffectColors.Gradient(new Point(0.5, 0), new Point(0.5, 1), stops, period);
    }

    /// <summary>
    /// Makes the mask that softens the beam at its left and right edges.
    /// </summary>
    /// <returns>The mask.</returns>
    private static LinearGradientBrush SideFade()
    {
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        foreach (var (offset, alpha) in new (double, byte)[] { (0, 0), (0.25, 0x99), (0.5, 0xFF), (0.75, 0x99), (1, 0) })
        {
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF), offset));
        }

        mask.Freeze();
        return mask;
    }
}
