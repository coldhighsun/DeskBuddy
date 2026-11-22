using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DeskBuddy.App.Windows;

/// <summary>
/// A puff of colored smoke, whose colors keep running round the rainbow, swells around the pet and hides it; the new pet is put in its place under the smoke, and the smoke
/// clears with a few sparks.
/// </summary>
internal sealed class SmokeEffect : IPetSwitchEffect
{
    /// <summary>
    /// The puffs of the cloud: where each is, sideways and up, and how big it is, as multiples of the pet's larger side.
    /// </summary>
    private static readonly (double X, double Y, double Size)[] Puffs =
    [
        (0, 0, 0.85),
        (-0.4, 0.05, 0.65),
        (0.4, 0, 0.7),
        (-0.2, -0.38, 0.6),
        (0.22, -0.4, 0.6),
        (-0.3, 0.38, 0.55),
        (0.32, 0.36, 0.55),
    ];

    /// <summary>
    /// The sparks: where each is around the middle of the pet, as multiples of the pet's larger side.
    /// </summary>
    private static readonly (double X, double Y)[] Sparks = [(-0.62, -0.35), (0.6, -0.4), (-0.55, 0.3), (0.65, 0.25), (0, -0.7)];

    /// <summary>
    /// The pieces of the cloud, each with the scale transform it grows by.
    /// </summary>
    private readonly List<(UIElement Piece, ScaleTransform Scale)> _cloud = [];

    /// <summary>
    /// The sparks, each with the scale transform it grows by.
    /// </summary>
    private readonly List<(UIElement Piece, ScaleTransform Scale)> _sparks = [];

    /// <summary>
    /// The height of the middle of the pet when the cloud was made.
    /// </summary>
    private double _centerY;

    /// <inheritdoc />
    public async Task LeaveAsync(EffectStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var layout = stage.Layout;
        var unit = layout.Unit;
        _centerY = layout.PetTop + (layout.PetHeight / 2);

        var index = 0;
        foreach (var (x, y, size) in Puffs)
        {
            var diameter = size * unit;
            var puff = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = EffectColors.Puff((double)index++ / Puffs.Length, TimeSpan.FromMilliseconds(2400), 0.3),
                Opacity = 0,
            };
            _cloud.Add((Place(stage, puff, layout.CenterX + (x * unit), _centerY + (y * unit), diameter), (ScaleTransform)puff.RenderTransform));
        }

        foreach (var (x, y) in Sparks)
        {
            var diameter = 0.22 * unit;
            var spark = new Path
            {
                Data = Geometry.Parse("M 0,-10 L 2.5,-2.5 L 10,0 L 2.5,2.5 L 0,10 L -2.5,2.5 L -10,0 L -2.5,-2.5 Z"),
                Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xD8, 0x4A)),
                Width = diameter,
                Height = diameter,
                Stretch = Stretch.Fill,
                Opacity = 0,
            };
            _sparks.Add((Place(stage, spark, layout.CenterX + (x * unit), _centerY + (y * unit), diameter), (ScaleTransform)spark.RenderTransform));
        }

        var grow = new List<Task>();
        foreach (var (piece, scale) in _cloud.Concat(_sparks))
        {
            grow.Add(stage.ScaleAsync(scale, 1, 260, EffectStage.Overshoot));
            grow.Add(stage.AnimateAsync(piece, UIElement.OpacityProperty, 0.97, 220));
        }

        await Task.WhenAll(grow);

        // Hidden behind the smoke.
        stage.Pet.Opacity = 0;
    }

    /// <inheritdoc />
    public async Task EnterAsync(EffectStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        // The new pet may be of another size, which moves its middle as its feet stay where they are.
        var layout = stage.Layout;
        var shift = layout.PetTop + (layout.PetHeight / 2) - _centerY;
        foreach (var (piece, _) in _cloud.Concat(_sparks))
        {
            Canvas.SetTop(piece, Canvas.GetTop(piece) + shift);
        }

        // The pet is shown at once; it is the smoke that hides it until it clears.
        stage.Pet.Opacity = 1;
        var clear = new List<Task>();
        foreach (var (piece, scale) in _cloud.Concat(_sparks))
        {
            clear.Add(stage.ScaleAsync(scale, 1.5, 380, EffectStage.EaseOut));
            clear.Add(stage.AnimateAsync(piece, UIElement.OpacityProperty, 0, 380, EffectStage.EaseIn));
        }

        await Task.WhenAll(clear);
    }

    /// <summary>
    /// Puts a piece of the cloud on the canvas, centered on a point and growing from its middle.
    /// </summary>
    /// <param name="stage">What the piece is put on.</param>
    /// <param name="piece">The piece; its size is already set.</param>
    /// <param name="centerX">The horizontal middle of the piece.</param>
    /// <param name="centerY">The vertical middle of the piece.</param>
    /// <param name="size">The size of the piece, to center it.</param>
    /// <returns>The piece.</returns>
    private static UIElement Place(EffectStage stage, FrameworkElement piece, double centerX, double centerY, double size)
    {
        piece.RenderTransformOrigin = new Point(0.5, 0.5);
        piece.RenderTransform = new ScaleTransform(0.1, 0.1);
        Canvas.SetLeft(piece, centerX - (size / 2));
        Canvas.SetTop(piece, centerY - (size / 2));
        stage.Canvas.Children.Add(piece);
        return piece;
    }
}
