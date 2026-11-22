using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DeskBuddy.App.Windows;

/// <summary>
/// A crane cradle comes down from the top of the screen on its cable, with an arm on each side of the pet. Two plates swing
/// in under the pet's feet and the cradle carries the pet out of the screen. The new pet is brought in the same way, set
/// down in the old pet's place, and the plates swing away and the cradle goes back up. Nothing of it is ever in front of the
/// pet's body: the arms are beside the pet's frame, and the plates only move below its feet.
/// </summary>
internal sealed class ClawEffect : IPetSwitchEffect
{
    /// <summary>
    /// How far the plates hang from the horizontal when open, in degrees; a little past straight down, so they hang out of
    /// the way beside the arms.
    /// </summary>
    private const double OpenAngle = 100;

    /// <summary>
    /// How far, in device-independent pixels, the cradle and the pet are lifted beyond the top of the window to be out of
    /// sight.
    /// </summary>
    private const double Clearance = 24;

    /// <summary>
    /// Moves the cradle alone, as it is lowered and drawn back.
    /// </summary>
    private TranslateTransform _drop = new();

    /// <summary>
    /// Turns the left plate about the foot of its arm; zero is flat under the pet.
    /// </summary>
    private RotateTransform _left = new(OpenAngle);

    /// <summary>
    /// Turns the right plate about the foot of its arm; zero is flat under the pet.
    /// </summary>
    private RotateTransform _right = new(-OpenAngle);

    /// <summary>
    /// The cradle with its cable, drawn at the place where it holds the pet.
    /// </summary>
    private Canvas _claw = new();

    /// <summary>
    /// How far above the window the cradle has to be to be out of sight, which is how far down its lowest part reaches.
    /// </summary>
    private double _reach;

    /// <inheritdoc />
    public async Task LeaveAsync(EffectStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        Draw(stage, open: true);
        _drop.Y = -_reach;

        // Comes down with an arm on each side of the pet, then the plates swing in under its feet.
        await stage.AnimateAsync(_drop, TranslateTransform.YProperty, 0, 500, EffectStage.EaseOut);
        await Task.WhenAll(
            stage.AnimateAsync(_left, RotateTransform.AngleProperty, 0, 200),
            stage.AnimateAsync(_right, RotateTransform.AngleProperty, 0, 200));

        // Carried: the pet sits on the plates, shown by its stand-in, and goes up out of the screen with the cradle.
        stage.Pet.Opacity = 0;
        stage.Clone.Opacity = 1;
        await stage.AnimateAsync(stage.CloneShift, TranslateTransform.YProperty, -(stage.Layout.PetBottom + Clearance), 550, EffectStage.EaseIn);
        stage.Clone.Opacity = 0;
    }

    /// <inheritdoc />
    public async Task EnterAsync(EffectStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        // The new pet may be of another size, so the cradle is made again for it. It starts above the screen, carrying the
        // pet on its plates.
        stage.Canvas.Children.Remove(_claw);
        Draw(stage, open: false);
        stage.ResetClone();
        stage.CloneShift.Y = -(stage.Layout.PetBottom + Clearance);
        stage.Clone.Opacity = 1;

        await stage.AnimateAsync(stage.CloneShift, TranslateTransform.YProperty, 0, 550, EffectStage.EaseOut);

        // Set down: the plates swing away from under the pet, which settles with a small squash, and the pet's own window
        // takes over.
        await Task.WhenAll(
            stage.AnimateAsync(_left, RotateTransform.AngleProperty, OpenAngle, 220),
            stage.AnimateAsync(_right, RotateTransform.AngleProperty, -OpenAngle, 220),
            Squash(stage));
        stage.Pet.Opacity = 1;
        stage.Clone.Opacity = 0;

        await stage.AnimateAsync(_drop, TranslateTransform.YProperty, -_reach, 400, EffectStage.EaseIn);
    }

    /// <summary>
    /// Makes the pet's stand-in squash a little and spring back, as it lands.
    /// </summary>
    /// <param name="stage">What the animation moves.</param>
    /// <returns>A task that completes when it has sprung back.</returns>
    private static async Task Squash(EffectStage stage)
    {
        await stage.AnimateAsync(stage.CloneScale, ScaleTransform.ScaleYProperty, 0.92, 90, EffectStage.EaseIn);
        await stage.AnimateAsync(stage.CloneScale, ScaleTransform.ScaleYProperty, 1, 130, EffectStage.Overshoot);
    }

    /// <summary>
    /// Draws the cradle round the pet in the layout and puts it on the canvas: a beam above the pet's head on the cable, an
    /// arm from each end of the beam down past the pet's sides, and under the pet's feet a plate hinged to the foot of each
    /// arm.
    /// </summary>
    /// <param name="stage">What the cradle is drawn on.</param>
    /// <param name="open">Whether the plates start swung away from under the pet, or flat under it.</param>
    private void Draw(EffectStage stage, bool open)
    {
        var layout = stage.Layout;
        var unit = layout.Unit;
        var thickness = Math.Max(3, 0.05 * unit);

        // The arms stand beside the pet's frame, so they can never be in front of the pet.
        var spread = (layout.PetWidth / 2) + Math.Max(3, 0.08 * unit) + (thickness / 2);
        var beamHeight = Math.Max(4, 0.08 * unit);
        var beamTop = layout.PetTop - Math.Max(3, 0.08 * unit) - beamHeight;

        // The plates are hinged just below the feet, and long enough to reach past the middle.
        var hinge = layout.PetBottom + (thickness / 2) + 1.5;
        var plate = spread + (0.05 * layout.PetWidth);
        _reach = hinge + plate + Clearance;

        _drop = new TranslateTransform();
        _left = new RotateTransform(open ? OpenAngle : 0);
        _right = new RotateTransform(open ? -OpenAngle : 0);
        _claw = new Canvas { RenderTransform = new TransformGroup { Children = { _drop, stage.CloneShift } } };
        Canvas.SetLeft(_claw, layout.CenterX);

        // Painted like an arcade machine: red beam and arms, yellow plates, a dark cable.
        var red = new SolidColorBrush(Color.FromRgb(0xE0, 0x3B, 0x3B));
        var yellow = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x2E));
        var dark = new SolidColorBrush(Color.FromRgb(0x4A, 0x52, 0x5C));
        var cable = Math.Max(2, 0.03 * unit);

        // The cable reaches up far beyond the top of the window.
        _claw.Children.Add(Block(-cable / 2, -3000, cable, 3000 + beamTop, dark));
        _claw.Children.Add(Block(-spread - (thickness / 2), beamTop, (2 * spread) + thickness, beamHeight, red));
        _claw.Children.Add(Block(-spread - (thickness / 2), beamTop + (beamHeight / 2), thickness, hinge - beamTop - (beamHeight / 2), red));
        _claw.Children.Add(Block(spread - (thickness / 2), beamTop + (beamHeight / 2), thickness, hinge - beamTop - (beamHeight / 2), red));
        _claw.Children.Add(Plate(-spread, hinge, plate, thickness, yellow, _left));
        _claw.Children.Add(Plate(spread, hinge, -plate, thickness, yellow, _right));
        stage.Canvas.Children.Add(_claw);
    }

    /// <summary>
    /// Makes a rectangle.
    /// </summary>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="fill">What fills it.</param>
    /// <returns>The rectangle.</returns>
    private static Rectangle Block(double left, double top, double width, double height, Brush fill)
    {
        var block = new Rectangle { Width = width, Height = height, Fill = fill, RadiusX = 1.5, RadiusY = 1.5 };
        Canvas.SetLeft(block, left);
        Canvas.SetTop(block, top);
        return block;
    }

    /// <summary>
    /// Makes a plate: a bar hinged at the foot of an arm that swings about the hinge.
    /// </summary>
    /// <param name="hingeX">Where the hinge is, sideways from the middle of the cradle.</param>
    /// <param name="hingeY">Where the hinge is, down from the top of the window.</param>
    /// <param name="length">How long the plate is; negative for a plate that points to the left when flat.</param>
    /// <param name="thickness">How thick the plate is.</param>
    /// <param name="brush">The color of the plate.</param>
    /// <param name="angle">The turn of the plate about its hinge.</param>
    /// <returns>The plate.</returns>
    private static Line Plate(double hingeX, double hingeY, double length, double thickness, Brush brush, RotateTransform angle)
    {
        var plate = new Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = length,
            Y2 = 0,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            RenderTransform = angle,
        };
        Canvas.SetLeft(plate, hingeX);
        Canvas.SetTop(plate, hingeY);
        return plate;
    }
}
