using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Click-through window drawn over the pet while it falls: an umbrella or parachute that opens when the fall speeds up
/// and folds away on landing, with its shaft or strings reaching down to where the pet holds on (the pet's own grips,
/// see <see cref="CanopyRig"/>). It is kept just behind the pet window, so the paws cover the ends of the shaft and
/// the strings. Drawn in code, so it works with every pet.
/// </summary>
public partial class UmbrellaWindow : Window
{
    /// <summary>
    /// How long the umbrella takes to open or fold.
    /// </summary>
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(260);

    /// <summary>
    /// Where along the rim, in units of the canopy drawing, the parachute's strings start, from left to right.
    /// </summary>
    private static readonly Point[] StringAnchors = [new(12, 50), new(42, 52), new(70, 54), new(98, 52), new(128, 50)];

    /// <summary>
    /// The colour of the umbrella's shaft.
    /// </summary>
    private static readonly Brush ShaftBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5A, 0x3A, 0x28)));

    /// <summary>
    /// The colour of the parachute's strings.
    /// </summary>
    private static readonly Brush StringBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A)));

    /// <summary>
    /// What is open and where the pet holds on to it.
    /// </summary>
    private CanopyRig _rig = CanopyRig.Default;

    /// <summary>
    /// The layout the shaft or strings were last drawn for, so they are only drawn again when it changes.
    /// </summary>
    private (CanopyRig Rig, double Width, double Height)? _drawnFor;

    /// <summary>
    /// Whether the canopy is folding away, so a fold that was overtaken by <see cref="Open"/> does not hide the window.
    /// </summary>
    private bool _folding;

    /// <summary>
    /// Whether the window has been closed; a fold that finishes after that must not touch it.
    /// </summary>
    private bool _closed;

    /// <summary>
    /// The last position, in device pixels, the window was moved to, so a step that does not change it costs no move.
    /// </summary>
    private (int X, int Y)? _lastPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="UmbrellaWindow"/> class.
    /// </summary>
    public UmbrellaWindow()
    {
        InitializeComponent();

        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) => _closed = true;

        // The window is placed in device pixels before it is first shown, which needs its handle.
        _ = new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>
    /// Opens the umbrella or parachute over the pet.
    /// </summary>
    /// <param name="rig">
    /// What to open, and where the pet holds on to it.
    /// </param>
    /// <param name="pet">
    /// The pet window to anchor to.
    /// </param>
    public void Open(CanopyRig rig, Window pet)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(pet);

        _rig = rig;
        UmbrellaShape.Visibility = rig.Kind == UmbrellaKind.Umbrella ? Visibility.Visible : Visibility.Collapsed;
        ParachuteShape.Visibility = rig.Kind == UmbrellaKind.Parachute ? Visibility.Visible : Visibility.Collapsed;

        _folding = false;
        PositionAbove(pet);
        if (!IsVisible)
        {
            Show();

            // Showing may have moved the window, so the cached position cannot be trusted.
            _lastPosition = null;
            PositionAbove(pet);
        }

        WindowMover.PlaceBehind(this, pet);
        Animate(1, onCompleted: null);
    }

    /// <summary>
    /// Folds the umbrella or parachute away and hides the window.
    /// </summary>
    public void Fold()
    {
        if (!IsVisible)
        {
            return;
        }

        _folding = true;
        Animate(0.1, onCompleted: () =>
        {
            if (_folding && !_closed)
            {
                Hide();
            }
        });
    }

    /// <summary>
    /// Keeps the canopy centered over the pet's head. Placed in device pixels, as the speech bubble is, so it lands in
    /// the right place when the pet is on a monitor whose scaling differs from the one the canopy was last on.
    /// </summary>
    /// <param name="pet">
    /// The pet window to anchor to.
    /// </param>
    public void PositionAbove(Window pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        var placement = PrepareFor(pet, MonitorHelper.GetWindowBoundsPixels(pet), VisualTreeHelper.GetDpi(pet).DpiScaleX);
        if (IsAt(placement))
        {
            return;
        }

        WindowMover.MoveToPixels(this, placement.X, placement.Y);
        _lastPosition = (placement.X, placement.Y);
    }

    /// <summary>
    /// Moves the pet to a new place and the canopy over it to its new place, in one call to the window manager, so the two
    /// are drawn in the same frame instead of the canopy lagging a step behind a fast-falling pet.
    /// </summary>
    /// <param name="pet">
    /// The pet window to move and anchor to.
    /// </param>
    /// <param name="petTarget">
    /// Where the pet window is to be, in device pixels.
    /// </param>
    /// <param name="petScale">
    /// Device pixels per device-independent pixel on the pet's monitor.
    /// </param>
    public void MoveWithPet(Window pet, PixelRect petTarget, double petScale)
    {
        ArgumentNullException.ThrowIfNull(pet);

        var placement = PrepareFor(pet, petTarget, petScale);
        if (IsAt(placement))
        {
            WindowMover.MoveToPixels(pet, petTarget.Left, petTarget.Top);
            return;
        }

        WindowMover.MoveTogetherToPixels(pet, petTarget.Left, petTarget.Top, this, placement.X, placement.Y);
        _lastPosition = (placement.X, placement.Y);
    }

    /// <summary>
    /// Works out where the canopy goes for a pet at the given place, after bringing the window onto the pet's monitor
    /// when its scaling differs and giving it the size that goes with the pet.
    /// </summary>
    /// <param name="pet">
    /// The pet window to anchor to.
    /// </param>
    /// <param name="petBounds">
    /// Where the pet window is, or is about to be, in device pixels.
    /// </param>
    /// <param name="petScale">
    /// Device pixels per device-independent pixel on the pet's monitor.
    /// </param>
    /// <returns>
    /// The size and position of the canopy window.
    /// </returns>
    private CanopyPlacement PrepareFor(Window pet, PixelRect petBounds, double petScale)
    {
        var placement = CanopyPlacement.For(petBounds, pet.Width, petScale, _rig);

        // Sizes are set in device-independent units at the scale of the monitor the window is on, so bring it onto the
        // pet's monitor first when the scaling differs; the size then comes out right.
        if (Math.Abs(VisualTreeHelper.GetDpi(this).DpiScaleX - petScale) > 0.001)
        {
            WindowMover.MoveToPixels(this, petBounds.Left, petBounds.Top);
            _lastPosition = null;
        }

        if (Width != placement.Width || Height != placement.Height)
        {
            Width = placement.Width;
            Height = placement.Height;
        }

        if (_drawnFor != (_rig, placement.Width, placement.Height))
        {
            DrawRigging(placement);
            _drawnFor = (_rig, placement.Width, placement.Height);
        }

        return placement;
    }

    /// <summary>
    /// Sizes the dome and draws the shaft or the strings from it down to the pet's grips.
    /// </summary>
    /// <param name="placement">
    /// The canopy window's layout.
    /// </param>
    private void DrawRigging(CanopyPlacement placement)
    {
        var scale = placement.Scale;
        DomeBox.Width = placement.Width;
        DomeBox.Height = CanopyPlacement.DomeHeight * scale;

        var figures = new PathGeometry();
        if (_rig.Kind == UmbrellaKind.Umbrella)
        {
            // From under the dome straight down to the paw, and a little past it, so the paw closes round it.
            var grip = placement.Grips.MaxBy(point => point.X);
            figures.Figures.Add(Line(new Point(grip.X, 30 * scale), new Point(grip.X, grip.Y + (3 * scale))));
            Rigging.Stroke = ShaftBrush;
            Rigging.StrokeThickness = 3.5 * scale;
        }
        else
        {
            // With one grip every string meets there. With two, the strings on the left go to the left one and those on
            // the right to the right one, and the middle string, which would cross over the head, is left out.
            var left = placement.Grips.MinBy(point => point.X);
            var right = placement.Grips.MaxBy(point => point.X);
            var single = placement.Grips.Count == 1;
            var middle = StringAnchors.Length / 2;
            for (var i = 0; i < StringAnchors.Length; i++)
            {
                if (!single && i == middle)
                {
                    continue;
                }

                var start = new Point(StringAnchors[i].X * scale, StringAnchors[i].Y * scale);
                figures.Figures.Add(Line(start, single || i < middle ? left : right));
            }

            Rigging.Stroke = StringBrush;
            Rigging.StrokeThickness = 1.4 * scale;
        }

        figures.Freeze();
        Rigging.Data = figures;
    }

    /// <summary>
    /// A straight line as a path figure.
    /// </summary>
    /// <param name="from">
    /// Where it starts.
    /// </param>
    /// <param name="to">
    /// Where it ends.
    /// </param>
    /// <returns>
    /// The figure.
    /// </returns>
    private static PathFigure Line(Point from, Point to)
    {
        return new PathFigure(from, [new LineSegment(to, isStroked: true)], closed: false);
    }

    /// <summary>
    /// Freezes a brush, so it can be shared.
    /// </summary>
    /// <param name="brush">
    /// The brush.
    /// </param>
    /// <returns>
    /// The same brush, frozen.
    /// </returns>
    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Tells whether the canopy window is already at the place a placement asks for.
    /// </summary>
    /// <param name="placement">
    /// The placement.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the window was last moved to that place.
    /// </returns>
    private bool IsAt(CanopyPlacement placement)
    {
        return _lastPosition == (placement.X, placement.Y);
    }

    /// <summary>
    /// Scales the canopy to the given factor.
    /// </summary>
    /// <param name="target">
    /// The scale to reach.
    /// </param>
    /// <param name="onCompleted">
    /// Called when the animation has finished, or <see langword="null"/>.
    /// </param>
    private void Animate(double target, Action? onCompleted)
    {
        var easing = new BackEase { EasingMode = target > 0.5 ? EasingMode.EaseOut : EasingMode.EaseIn, Amplitude = 0.4 };
        var animation = new DoubleAnimation(target, AnimationDuration) { EasingFunction = easing };
        if (onCompleted is not null)
        {
            animation.Completed += (_, _) => onCompleted();
        }

        PropScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        PropScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone());

        // The shaft or the strings do not grow with the dome, as their ends stay at the paws; they fade in and out.
        Rigging.BeginAnimation(OpacityProperty, new DoubleAnimation(target > 0.5 ? 1 : 0, AnimationDuration));
    }

    /// <summary>
    /// Configures the native window once its handle exists, the same way as the pet window, and makes it click-through.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!;
        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        source.AddHook(HitTestHook);

        WindowStyleHelper.MakeToolWindowNoActivate(this);
        WindowStyleHelper.RemoveDwmDecorations(this);
    }

    /// <summary>
    /// Lets every mouse click pass through the window.
    /// </summary>
    /// <param name="hwnd">
    /// The window handle.
    /// </param>
    /// <param name="msg">
    /// The message.
    /// </param>
    /// <param name="wParam">
    /// The first message parameter.
    /// </param>
    /// <param name="lParam">
    /// The second message parameter.
    /// </param>
    /// <param name="handled">
    /// Set when the message was dealt with.
    /// </param>
    /// <returns>
    /// <c>HTTRANSPARENT</c> for hit tests, otherwise zero.
    /// </returns>
    private static nint HitTestHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WindowStyleHelper.WmNcHitTest)
        {
            handled = true;
            return WindowStyleHelper.HtTransparent;
        }

        return 0;
    }
}
