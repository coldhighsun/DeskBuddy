using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Rendering;
using DeskBuddy.Application;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Skins;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Transparent, always-on-top window that renders the pet sprite, reacts to the mouse and walks along the bottom of
/// the work area.
/// </summary>
public partial class PetWindow : Window
{
    /// <summary>
    /// Margin, in device-independent pixels, kept between the pet and the screen edge at startup.
    /// </summary>
    private const double StartupMargin = 24;

    /// <summary>
    /// Distance, in device-independent pixels, the mouse must travel with the button held before a press becomes a
    /// drag instead of a click.
    /// </summary>
    private const double DragThreshold = 4;

    /// <summary>
    /// How often the pet's position is updated while walking. Only runs in the Walk state.
    /// </summary>
    private static readonly TimeSpan MoveInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>
    /// How often the brain re-reads sensors and re-evaluates the pet's state.
    /// </summary>
    private static readonly TimeSpan BrainInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Plays the current animation of the skin.
    /// </summary>
    private readonly AnimationPlayer _player;

    /// <summary>
    /// Decoded frames of the skin.
    /// </summary>
    private readonly SkinFrameCache _frames;

    /// <summary>
    /// Decides what the pet is doing.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Behavior tuning, used here for the walking speed.
    /// </summary>
    private readonly PetBehaviorOptions _options;

    /// <summary>
    /// Timer that ticks once per animation frame. Used instead of <c>CompositionTarget.Rendering</c>, which forces
    /// continuous 60 Hz redraws and burns CPU.
    /// </summary>
    private readonly DispatcherTimer _animationTimer = new(DispatcherPriority.Render);

    /// <summary>
    /// Timer that re-evaluates the pet's state.
    /// </summary>
    private readonly DispatcherTimer _brainTimer = new() { Interval = BrainInterval };

    /// <summary>
    /// Timer that moves the window while walking; stopped in every other state.
    /// </summary>
    private readonly DispatcherTimer _moveTimer = new(DispatcherPriority.Render) { Interval = MoveInterval };

    /// <summary>
    /// Measures real elapsed time between animation ticks, since timer intervals are not exact.
    /// </summary>
    private readonly Stopwatch _animationWatch = new();

    /// <summary>
    /// Measures real elapsed time between movement ticks.
    /// </summary>
    private readonly Stopwatch _moveWatch = new();

    /// <summary>
    /// Number of movement ticks between refreshes of the cached work area (about one second).
    /// </summary>
    private const int WorkAreaRefreshEveryTicks = 30;

    /// <summary>
    /// Work area of the monitor the pet is on, cached while walking.
    /// </summary>
    private Rect _workArea;

    /// <summary>
    /// Movement ticks since the work area was last refreshed; reset when a walk starts.
    /// </summary>
    private int _moveTicksSinceWorkAreaRefresh;

    /// <summary>
    /// The state currently shown, to detect changes.
    /// </summary>
    private PetState _shownState = PetState.Idle;

    /// <summary>
    /// Walking direction: <c>-1</c> for left, <c>1</c> for right. Also determines which way the sprite faces.
    /// </summary>
    private int _direction = 1;

    /// <summary>
    /// Whether the left mouse button is currently held on the pet.
    /// </summary>
    private bool _pressed;

    /// <summary>
    /// Whether the current press has turned into a drag.
    /// </summary>
    private bool _dragging;

    /// <summary>
    /// Cursor position, in DIPs, where the current press started.
    /// </summary>
    private Point _pressPoint;

    /// <summary>
    /// Offset, in DIPs, from the window's top-left corner to the cursor at the start of the press.
    /// </summary>
    private Vector _grabOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetWindow"/> class.
    /// </summary>
    /// <param name="skin">
    /// The skin that determines the window size and animations.
    /// </param>
    /// <param name="frames">
    /// Decoded frames for the skin.
    /// </param>
    /// <param name="brain">
    /// The brain that decides what the pet does.
    /// </param>
    /// <param name="options">
    /// Behavior tuning.
    /// </param>
    public PetWindow(Skin skin, SkinFrameCache frames, PetBrain brain, PetBehaviorOptions options)
    {
        InitializeComponent();

        _player = new AnimationPlayer(skin);
        _frames = frames;
        _brain = brain;
        _options = options;

        Width = skin.FrameWidth;
        Height = skin.FrameHeight;
        ShowFrame();

        SourceInitialized += OnSourceInitialized;
        _animationTimer.Interval = _player.Animation.FrameDuration;
        _animationTimer.Tick += OnAnimationTick;
        _brainTimer.Tick += OnBrainTick;
        _moveTimer.Tick += OnMoveTick;
        Loaded += (_, _) =>
        {
            _animationWatch.Restart();
            _animationTimer.Start();
            _brainTimer.Start();
        };
        Closed += (_, _) =>
        {
            _animationTimer.Stop();
            _brainTimer.Stop();
            _moveTimer.Stop();
        };

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        PlaceAtBottomRight();
    }

    /// <summary>
    /// Configures the native window once its handle exists: transparent composition target, tool-window style,
    /// no DWM border, and the hit-test hook that makes transparent pixels click-through.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        source.AddHook(WndProc);

        WindowStyleHelper.MakeToolWindowNoActivate(this);
        WindowStyleHelper.RemoveDwmDecorations(this);
    }

    /// <summary>
    /// Window procedure hook: answers <c>WM_NCHITTEST</c> so mouse events over transparent pixels reach the window
    /// underneath, while events over the pet itself are handled here.
    /// </summary>
    /// <param name="hwnd">
    /// Window handle.
    /// </param>
    /// <param name="msg">
    /// Window message.
    /// </param>
    /// <param name="wParam">
    /// Message <c>wParam</c>.
    /// </param>
    /// <param name="lParam">
    /// Message <c>lParam</c>.
    /// </param>
    /// <param name="handled">
    /// Set to <see langword="true"/> when the message was processed.
    /// </param>
    /// <returns>
    /// The hit-test result for <c>WM_NCHITTEST</c>; otherwise zero.
    /// </returns>
    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WindowStyleHelper.WmNcHitTest)
        {
            return 0;
        }

        var (x, y) = WindowStyleHelper.ScreenLParamToClient(hwnd, lParam);
        var dpi = VisualTreeHelper.GetDpi(this);
        var widthPx = ActualWidth * dpi.DpiScaleX;
        var heightPx = ActualHeight * dpi.DpiScaleY;

        var hit = false;
        if (widthPx > 0 && heightPx > 0)
        {
            // The sprite is mirrored when facing left, so mirror the lookup too.
            var u = x / widthPx;
            if (FlipTransform.ScaleX < 0)
            {
                u = 1 - u;
            }

            hit = _frames.GetMask(_player.CurrentFramePath).IsOpaqueAtNormalized(u, y / heightPx);
        }

        handled = true;
        return hit ? WindowStyleHelper.HtClient : WindowStyleHelper.HtTransparent;
    }

    /// <summary>
    /// Positions the window in the bottom-right corner of the primary work area (above the taskbar).
    /// </summary>
    private void PlaceAtBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - StartupMargin;
        Top = workArea.Bottom - Height;
    }

    /// <summary>
    /// Switches the displayed state: swaps the animation, and starts or stops the walking timer.
    /// </summary>
    /// <param name="state">
    /// The state to show.
    /// </param>
    private void ApplyState(PetState state)
    {
        if (state == _shownState)
        {
            return;
        }

        _shownState = state;

        if (_player.SetState(state))
        {
            ShowFrame();
        }

        _animationWatch.Restart();
        _animationTimer.Interval = _player.Animation.FrameDuration;

        if (state == PetState.Walk)
        {
            _direction = _brain.WalkDirection;
            ApplyFacing();
            _moveTicksSinceWorkAreaRefresh = 0;
            _moveWatch.Restart();
            _moveTimer.Start();
        }
        else
        {
            _moveTimer.Stop();
        }
    }

    /// <summary>
    /// Mirrors the sprite so it faces the walking direction.
    /// </summary>
    private void ApplyFacing()
    {
        FlipTransform.ScaleX = _direction < 0 ? -1 : 1;
    }

    /// <summary>
    /// Advances the animation by the real elapsed time and swaps the sprite when the frame changes.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var delta = _animationWatch.Elapsed;
        _animationWatch.Restart();

        if (_player.Advance(delta))
        {
            ShowFrame();
        }
    }

    /// <summary>
    /// Lets the brain re-evaluate the pet's state and shows the result.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnBrainTick(object? sender, EventArgs e)
    {
        ApplyState(_brain.Tick());
    }

    /// <summary>
    /// Moves the pet along the bottom of the work area, turning around at its ends.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnMoveTick(object? sender, EventArgs e)
    {
        var delta = _moveWatch.Elapsed;
        _moveWatch.Restart();

        // The work area only changes when the taskbar or monitors change, so refresh it about once per second
        // instead of on every movement tick.
        if (_moveTicksSinceWorkAreaRefresh++ % WorkAreaRefreshEveryTicks == 0)
        {
            _workArea = MonitorHelper.GetWorkAreaDip(this);
        }

        var (x, direction) = WalkMovement.Step(
            Left,
            _direction,
            _options.WalkSpeed,
            delta,
            _workArea.Left,
            _workArea.Right - Width);

        // Each of Left/Top costs a SetWindowPos call, so only touch Top when it actually changed.
        var top = _workArea.Bottom - Height;
        if (Math.Abs(Top - top) > 0.5)
        {
            Top = top;
        }

        Left = x;

        if (direction != _direction)
        {
            _direction = direction;
            ApplyFacing();
        }
    }

    /// <summary>
    /// Shows the player's current frame in the sprite image.
    /// </summary>
    private void ShowFrame()
    {
        SpriteImage.Source = _frames.Get(_player.CurrentFramePath);
    }

    /// <summary>
    /// Starts tracking a press on the pet; it becomes a click or a drag depending on how the mouse moves.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Mouse event data.
    /// </param>
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _dragging = false;
        _pressPoint = MonitorHelper.GetCursorPositionDip(this);
        _grabOffset = _pressPoint - new Point(Left, Top);
        CaptureMouse();
    }

    /// <summary>
    /// Turns a press into a drag once the mouse has moved far enough, and moves the pet with the cursor.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Mouse event data.
    /// </param>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        var cursor = MonitorHelper.GetCursorPositionDip(this);
        if (!_dragging && (cursor - _pressPoint).Length >= DragThreshold)
        {
            _dragging = true;
            _brain.OnDragStarted();
            ApplyState(_brain.State);
        }

        if (_dragging)
        {
            Left = cursor.X - _grabOffset.X;
            Top = cursor.Y - _grabOffset.Y;
        }
    }

    /// <summary>
    /// Ends a press: a drop snaps the pet back onto the taskbar line, a short press counts as a click.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Mouse event data.
    /// </param>
    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        ReleaseMouseCapture();

        if (_dragging)
        {
            _dragging = false;
            _brain.OnDragEnded();
            SnapToTaskbar();
        }
        else
        {
            _brain.OnClicked();
        }

        ApplyState(_brain.State);
    }

    /// <summary>
    /// Places the pet on the taskbar line of the monitor it was dropped on, keeping it fully on that monitor.
    /// </summary>
    private void SnapToTaskbar()
    {
        var workArea = MonitorHelper.GetWorkAreaDip(this);
        Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = workArea.Bottom - Height;
    }
}
