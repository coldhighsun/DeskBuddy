using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DeskBuddy.App.Windows;

/// <summary>
/// A column of light that keeps changing color comes down on the pet and a bright line sweeps up through it, taking the pet away as it goes; then the
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
    /// The colors of the column, the glow and the core of the line, each made once for the whole switch: they keep running
    /// round the rainbow while the beam is gone and comes back, so the beam that shows the new pet is in the colors the old
    /// one was in, with no jump.
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

        // The new pet may be of another size, so the column and the line are made again for it.
        var layout = stage.Layout;
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
        _column.Fill = _columnFill ??= Rainbow(0x70, 0, TimeSpan.FromSeconds(2));
        Canvas.SetLeft(_column, layout.CenterX - (columnWidth / 2));
        Canvas.SetTop(_column, 0);

        var lineWidth = 1.4 * layout.PetWidth;
        var thickness = Math.Max(2, 0.035 * layout.PetHeight);
        _line.Children.Clear();
        _line.Opacity = 1;
        _line.RenderTransform = _sweep;
        _line.Children.Add(Bar(lineWidth, thickness * 5, -thickness * 2.5, _glowFill ??= Rainbow(0x50, 0.5, TimeSpan.FromSeconds(1))));
        _line.Children.Add(Bar(lineWidth, thickness, 0, _coreFill ??= Rainbow(0xFF, 0.2, TimeSpan.FromSeconds(1))));
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
        var bar = new Rectangle { Width = width, Height = height, Fill = brush };
        Canvas.SetTop(bar, top);
        return bar;
    }

    /// <summary>
    /// Makes a brush that is solid at the middle and clear at the sides, whose color runs round the rainbow.
    /// </summary>
    /// <param name="middle">How opaque the middle is.</param>
    /// <param name="phase">The point of the rainbow the middle starts from.</param>
    /// <param name="period">How long a full turn of the rainbow takes.</param>
    /// <returns>The brush.</returns>
    private static LinearGradientBrush Rainbow(byte middle, double phase, TimeSpan period)
    {
        var soft = (byte)(middle * 3 / 5);
        return EffectColors.Gradient(
            new Point(0, 0.5),
            new Point(1, 0.5),
            [(0, 0, phase), (0.25, soft, phase + 0.15), (0.5, middle, phase), (0.75, soft, phase + 0.15), (1, 0, phase)],
            period);
    }
}
