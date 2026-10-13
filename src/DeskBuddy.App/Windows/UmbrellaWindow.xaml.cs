using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Windows;

/// <summary>
/// What a falling pet holds over its head.
/// </summary>
public enum UmbrellaKind
{
    /// <summary>
    /// A red umbrella.
    /// </summary>
    Umbrella,

    /// <summary>
    /// A parachute.
    /// </summary>
    Parachute,
}

/// <summary>
/// Click-through window drawn over the pet's head while it falls: an umbrella or parachute that opens when the fall
/// speeds up and folds away on landing. Drawn in code, so it works with every pet.
/// </summary>
public partial class UmbrellaWindow : Window
{
    /// <summary>
    /// How long the umbrella takes to open or fold.
    /// </summary>
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(260);

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
    /// <param name="kind">
    /// What to open.
    /// </param>
    /// <param name="pet">
    /// The pet window to anchor to.
    /// </param>
    public void Open(UmbrellaKind kind, Window pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        UmbrellaShape.Visibility = kind == UmbrellaKind.Umbrella ? Visibility.Visible : Visibility.Collapsed;
        ParachuteShape.Visibility = kind == UmbrellaKind.Parachute ? Visibility.Visible : Visibility.Collapsed;

        _folding = false;
        PositionAbove(pet);
        if (!IsVisible)
        {
            Show();

            // Showing may have moved the window, so the cached position cannot be trusted.
            _lastPosition = null;
            PositionAbove(pet);
        }

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
        var placement = CanopyPlacement.For(petBounds, pet.Width, petScale);

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

        return placement;
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
