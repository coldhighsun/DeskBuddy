using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DeskBuddy.App.Windows;

/// <summary>
/// A flying saucer comes down from the top of the screen, beams the pet up into itself, waits there while the pet is
/// switched, beams the new pet down in the old pet's place and flies off.
/// </summary>
internal sealed class UfoEffect : IPetSwitchEffect
{
    /// <summary>
    /// The width of the saucer's drawing.
    /// </summary>
    private const double DrawingWidth = 150;

    /// <summary>
    /// The height of the saucer's drawing.
    /// </summary>
    private const double DrawingHeight = 64;

    /// <summary>
    /// How wide the saucer is, as a multiple of the pet's larger side.
    /// </summary>
    private const double SaucerWidthFactor = 1.6;

    /// <summary>
    /// How small the pet is when it is inside the saucer, as a fraction of its size.
    /// </summary>
    private const double BeamedScale = 0.12;

    /// <summary>
    /// The saucer, drawn at its hover place.
    /// </summary>
    private readonly Canvas _saucer = new() { Width = DrawingWidth, Height = DrawingHeight, RenderTransformOrigin = new Point(0.5, 0.5) };

    /// <summary>
    /// Moves the saucer in and out of the screen.
    /// </summary>
    private readonly TranslateTransform _flight = new();

    /// <summary>
    /// Tilts the saucer as it flies off.
    /// </summary>
    private readonly RotateTransform _tilt = new(0);

    /// <summary>
    /// The beam of light, rebuilt for each pet.
    /// </summary>
    private Polygon? _beam;

    /// <summary>
    /// The color of the beam, made once for the whole switch: its colors keep running round the rainbow while the beam is
    /// gone and comes back, so the beam that brings the new pet is in the colors the old one was in, with no jump.
    /// </summary>
    private LinearGradientBrush? _beamFill;

    /// <summary>
    /// The height at which the pet goes into the saucer, in the window.
    /// </summary>
    private double _belly;

    /// <summary>
    /// How far above its hover place the saucer is out of sight.
    /// </summary>
    private double _hiddenOffset;

    /// <inheritdoc />
    public async Task LeaveAsync(EffectStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var layout = stage.Layout;
        var width = SaucerWidthFactor * layout.Unit;
        var scale = width / DrawingWidth;
        var height = DrawingHeight * scale;

        // Hovers just above the pet, as far down as the top of the screen allows.
        var hoverTop = Math.Max(0, layout.PetTop - (1.1 * height));
        _belly = hoverTop + (0.8 * height);
        _hiddenOffset = -(hoverTop + height + 4);

        DrawSaucer();
        _saucer.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(scale, scale), _tilt, _flight },
        };
        Canvas.SetLeft(_saucer, layout.CenterX - (DrawingWidth / 2));
        Canvas.SetTop(_saucer, hoverTop + (height / 2) - (DrawingHeight / 2));
        _flight.Y = _hiddenOffset;

        _beam = BuildBeam(layout);
        stage.Canvas.Children.Insert(0, _beam);
        stage.Canvas.Children.Add(_saucer);

        await stage.AnimateAsync(_flight, TranslateTransform.YProperty, 0, 500, EffectStage.EaseOut);
        await stage.AnimateAsync(_beam, UIElement.OpacityProperty, 1, 150);

        // The pet is taken up into the belly of the saucer, shrinking as it rises.
        stage.Pet.Opacity = 0;
        stage.Clone.Opacity = 1;
        await Task.WhenAll(
            stage.AnimateAsync(stage.CloneShift, TranslateTransform.YProperty, UpDistance(layout), 600, EffectStage.EaseIn),
            stage.ScaleAsync(stage.CloneScale, BeamedScale, 600, EffectStage.EaseIn));
        stage.Clone.Opacity = 0;
        await stage.AnimateAsync(_beam, UIElement.OpacityProperty, 0, 120);
    }

    /// <inheritdoc />
    public async Task EnterAsync(EffectStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        var layout = stage.Layout;

        // The new pet may be of another size, so the beam is made again, and the picture starts inside the saucer.
        if (_beam is not null)
        {
            stage.Canvas.Children.Remove(_beam);
        }

        _beam = BuildBeam(layout);
        stage.Canvas.Children.Insert(0, _beam);
        stage.ResetClone();
        stage.CloneShift.Y = UpDistance(layout);
        stage.CloneScale.ScaleX = BeamedScale;
        stage.CloneScale.ScaleY = BeamedScale;
        stage.Clone.Opacity = 1;

        await stage.AnimateAsync(_beam, UIElement.OpacityProperty, 1, 120);
        await Task.WhenAll(
            stage.AnimateAsync(stage.CloneShift, TranslateTransform.YProperty, 0, 600, EffectStage.EaseOut),
            stage.ScaleAsync(stage.CloneScale, 1, 600, EffectStage.Overshoot));

        // Handed over to the pet's own window.
        stage.Pet.Opacity = 1;
        stage.Clone.Opacity = 0;
        await stage.AnimateAsync(_beam, UIElement.OpacityProperty, 0, 120);

        // Off it goes, tilting a little and picking up speed.
        await Task.WhenAll(
            stage.AnimateAsync(_flight, TranslateTransform.YProperty, _hiddenOffset, 450, EffectStage.EaseIn),
            stage.AnimateAsync(_tilt, RotateTransform.AngleProperty, 14, 450, EffectStage.EaseIn));
    }

    /// <summary>
    /// Works out how far above its place the pet is when it is at the belly of the saucer.
    /// </summary>
    /// <param name="layout">The layout of the pet.</param>
    /// <returns>The distance, negative for up.</returns>
    private double UpDistance(EffectLayout layout)
    {
        return _belly - (layout.PetTop + (layout.PetHeight / 2));
    }

    /// <summary>
    /// Makes the beam of light, from the belly of the saucer down to the pet's feet. Its color runs round the rainbow.
    /// </summary>
    /// <param name="layout">The layout of the pet.</param>
    /// <returns>The beam, invisible at first.</returns>
    private Polygon BuildBeam(EffectLayout layout)
    {
        var top = 0.22 * SaucerWidthFactor * layout.Unit;
        var bottom = Math.Max(0.75 * layout.PetWidth, top * 1.3);
        var foot = layout.PetBottom + 2;
        return new Polygon
        {
            Points =
            [
                new Point(layout.CenterX - top, _belly),
                new Point(layout.CenterX + top, _belly),
                new Point(layout.CenterX + bottom, foot),
                new Point(layout.CenterX - bottom, foot),
            ],
            Fill = _beamFill ??= EffectColors.Gradient(
                new Point(0.5, 0),
                new Point(0.5, 1),
                [(0, 0xD0, 0), (0.33, 0xA0, 0.25), (0.66, 0x70, 0.5), (1, 0x30, 0.75)],
                TimeSpan.FromMilliseconds(1600)),
            Opacity = 0,
        };
    }

    /// <summary>
    /// Draws the saucer: a glass dome on a silver disc with a ring of blinking lights.
    /// </summary>
    private void DrawSaucer()
    {
        _saucer.Children.Add(Oval(56, 40, 47, 2, Brush(0xB0, 0xCF, 0xEE, 0xFF), Brush(0xFF, 0x7F, 0xB8, 0xD8)));
        _saucer.Children.Add(Oval(70, 16, 40, 46, Brush(0xFF, 0x59, 0x60, 0x6B), Brush(0xFF, 0x3E, 0x44, 0x4D)));
        _saucer.Children.Add(Oval(
            150,
            36,
            0,
            20,
            new LinearGradientBrush(Color.FromRgb(0xE4, 0xE8, 0xEE), Color.FromRgb(0x8A, 0x93, 0xA0), 90),
            Brush(0xFF, 0x5F, 0x66, 0x70)));

        for (var i = 0; i < 5; i++)
        {
            var glow = i % 2 == 0 ? Brush(0xFF, 0xFF, 0xD8, 0x4A) : Brush(0xFF, 0xFF, 0x7A, 0x4A);
            var light = Oval(9, 9, 16 + (i * 26), 31, glow, null);
            light.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(260))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(i * 90),
                });
            _saucer.Children.Add(light);
        }
    }

    /// <summary>
    /// Makes an ellipse at a place of the saucer's drawing.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="fill">What fills it.</param>
    /// <param name="stroke">What outlines it, or <see langword="null"/> for no outline.</param>
    /// <returns>The ellipse.</returns>
    private static Ellipse Oval(double width, double height, double left, double top, Brush fill, Brush? stroke)
    {
        var oval = new Ellipse { Width = width, Height = height, Fill = fill, Stroke = stroke, StrokeThickness = stroke is null ? 0 : 1.5 };
        Canvas.SetLeft(oval, left);
        Canvas.SetTop(oval, top);
        return oval;
    }

    /// <summary>
    /// Makes a plain color brush.
    /// </summary>
    /// <param name="a">The opacity.</param>
    /// <param name="r">The red part.</param>
    /// <param name="g">The green part.</param>
    /// <param name="b">The blue part.</param>
    /// <returns>The brush.</returns>
    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b)
    {
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }
}
