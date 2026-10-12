using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.Resources;
using DeskBuddy.Application;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.Skins;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Transparent, always-on-top window that renders the pet sprite, reacts to the mouse and walks along the bottom of
/// the work area.
/// </summary>
public partial class PetWindow : Window, IScreenEdges, IPlacementTarget
{
    /// <summary>
    /// Distance, in device-independent pixels, the mouse must travel with the button held before a press becomes a
    /// drag instead of a click.
    /// </summary>
    private const double DragThreshold = 4;

    /// <summary>
    /// How long an animation picked from the preview menu plays.
    /// </summary>
    private static readonly TimeSpan PreviewDuration = TimeSpan.FromSeconds(6);

    /// <summary>
    /// How long the pet is dizzy after the mouse was shaken.
    /// </summary>
    private static readonly TimeSpan DizzyDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long the pet hangs on to the screen edge before it lets go.
    /// </summary>
    private static readonly TimeSpan HangDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the mouse must rest on the pet before it explains what it is doing, in milliseconds.
    /// </summary>
    private const int ToolTipDelayMilliseconds = 400;

    /// <summary>
    /// How often the mouse pointer is looked at, for shaking and for following it with the eyes.
    /// </summary>
    private static readonly TimeSpan PointerInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// How close to the left or right edge of the work area, in device-independent pixels, a dropped pet clings to it.
    /// Kept small, so a pet that merely walked to the edge is not mistaken for one dropped there. A new pet starts further
    /// from the edges than this.
    /// </summary>
    private const double EdgeSnapDistance = 8;

    /// <summary>
    /// How far from the left and right edges of the work area, in device-independent pixels, a new pet starts. Twice
    /// <see cref="EdgeSnapDistance"/>, so that a new pet is well outside the zone in which a dropped pet clings to the edge
    /// and a nudge does not make it climb or hang.
    /// </summary>
    private const double StartMargin = 2 * EdgeSnapDistance;

    /// <summary>
    /// How often the pet's position is updated while walking. Only runs in the Walk state.
    /// </summary>
    private static readonly TimeSpan MoveInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>
    /// How often the brain re-reads sensors and re-evaluates the pet's state.
    /// </summary>
    private static readonly TimeSpan BrainInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The skin being shown.
    /// </summary>
    private Skin _skin;

    /// <summary>
    /// How large the pet is shown.
    /// </summary>
    private PetSize _size;

    /// <summary>
    /// Plays the current animation of the skin.
    /// </summary>
    private AnimationPlayer _player;

    /// <summary>
    /// Which scenarios the user has switched on.
    /// </summary>
    private readonly ScenarioSwitches _switches;

    /// <summary>
    /// Timer that reads the mouse pointer for shaking and gaze; runs only while the pet is on screen.
    /// </summary>
    private readonly DispatcherTimer _pointerTimer = new() { Interval = PointerInterval };

    /// <summary>
    /// Decides from the pointer whether the pet gets dizzy or turns its eyes.
    /// </summary>
    private readonly PointerWatcher _pointer = new(new MouseShakeDetector());

    /// <summary>
    /// Tells where the pet is for the pointer watcher.
    /// </summary>
    private readonly Func<PetBox> _petBox;

    /// <summary>
    /// Runs for as long as the pointer is watched; gives the shake detector its time.
    /// </summary>
    private readonly Stopwatch _pointerWatch = Stopwatch.StartNew();

    /// <summary>
    /// Where the pet looks, on the screen.
    /// </summary>
    private GazeDirection _gaze = GazeDirection.None;

    /// <summary>
    /// The window rectangle in device pixels, read once after each move; <see langword="null"/> when it must be read
    /// again.
    /// </summary>
    private PixelRect? _pixelBounds;

    /// <summary>
    /// Whether the pet is clinging to the edge of the screen.
    /// </summary>
    private bool _hanging;

    /// <summary>
    /// Whether the user allows the pet to climb along the edges of the screen.
    /// </summary>
    private bool _climbEdges;

    /// <summary>
    /// Moves the pet along the sides and the top edge of the screen.
    /// </summary>
    private readonly EdgeCrawl _crawl;

    /// <summary>
    /// The motion of the fall in progress, or <see langword="null"/> when the pet is not falling.
    /// </summary>
    private FallState? _fall;

    /// <summary>
    /// The umbrella or parachute, created the first time a fall needs one.
    /// </summary>
    private UmbrellaWindow? _umbrella;

    /// <summary>
    /// Decoded frames of the skin.
    /// </summary>
    private SkinFrameCache _frames;

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
    /// Opens the panel window.
    /// </summary>
    private readonly Action<PanelTab?> _openPanel;

    /// <summary>
    /// Picks how the pet moves for the current disturbance mode.
    /// </summary>
    private readonly MovementSelector _movement;

    /// <summary>
    /// The speech bubble, created the first time a message needs showing.
    /// </summary>
    private BubbleWindow? _bubble;

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
    /// Which way the sprite faces: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    private int _facing = 1;

    /// <summary>
    /// Whether the context menu offers to preview every animation, for checking skins without waiting for the
    /// situations that trigger them.
    /// </summary>
    private readonly bool _previewMenu;

    /// <summary>
    /// Whether the left mouse button is currently held on the pet.
    /// </summary>
    private bool _pressed;

    /// <summary>
    /// Whether the current press has turned into a drag.
    /// </summary>
    private bool _dragging;

    /// <summary>
    /// Cursor position, in device pixels, where the current press started.
    /// </summary>
    private (int X, int Y) _pressPixel;

    /// <summary>
    /// Keeps the grab point under the cursor while dragging, in device pixels so it holds across monitors of different
    /// scaling.
    /// </summary>
    private readonly DragTracker _dragTracker = new();

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
    /// <param name="openPanel">
    /// Opens the panel window; called on double-click and from the context menu.
    /// </param>
    /// <param name="movement">
    /// Picks how the pet moves for the current disturbance mode.
    /// </param>
    /// <param name="switches">
    /// Which scenarios the user has switched on.
    /// </param>
    /// <param name="previewMenu">
    /// Whether the context menu offers to preview every animation.
    /// </param>
    public PetWindow(
        Skin skin,
        SkinFrameCache frames,
        PetBrain brain,
        PetBehaviorOptions options,
        Action<PanelTab?> openPanel,
        MovementSelector movement,
        ScenarioSwitches switches,
        PetSize size,
        bool previewMenu = false)
    {
        InitializeComponent();

        _skin = skin;
        _size = size;
        _switches = switches;
        _petBox = GetPetBox;
        ToolTipService.SetInitialShowDelay(SpriteImage, ToolTipDelayMilliseconds);
        _player = new AnimationPlayer(skin);
        _frames = frames;
        _brain = brain;
        _options = options;
        _crawl = new EdgeCrawl(options, Random.Shared);
        _openPanel = openPanel;
        _movement = movement;
        _previewMenu = previewMenu;

        Width = size.Scale(skin.FrameWidth);
        Height = size.Scale(skin.FrameHeight);
        ShowFrame();

        SourceInitialized += OnSourceInitialized;
        _animationTimer.Interval = _player.Animation.FrameDuration;
        _animationTimer.Tick += OnAnimationTick;
        _brainTimer.Tick += OnBrainTick;
        _moveTimer.Tick += OnMoveTick;
        _pointerTimer.Tick += OnPointerTick;
        Loaded += (_, _) =>
        {
            // The window was created at a spot worked out before it existed, perhaps on a monitor of another scaling: settle
            // now that it knows the scaling of the monitor it is on.
            SettleInWorkArea();

            _animationWatch.Restart();
            _animationTimer.Start();
            _brainTimer.Start();
            UpdatePointerTimer();
        };
        Closed += (_, _) =>
        {
            _pointerTimer.Stop();
            _animationTimer.Stop();
            _brainTimer.Stop();
            _moveTimer.Stop();
            _brain.MessageChanged -= OnMessageChanged;
            _brain.VisibilityChanged -= OnVisibilityChanged;
            _movement.ModeChanged -= OnMovementModeChanged;
            _bubble?.Close();
            _umbrella?.Close();
        };

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        _brain.MessageChanged += OnMessageChanged;
        _brain.VisibilityChanged += OnVisibilityChanged;
        _movement.ModeChanged += OnMovementModeChanged;
        IsVisibleChanged += (_, _) => OnMessageChanged(this, EventArgs.Empty);
        LocationChanged += (_, _) =>
        {
            _pixelBounds = null;
            RepositionBubble();
            RepositionUmbrella();
        };
        SizeChanged += (_, _) => _pixelBounds = null;
        DpiChanged += (_, _) => _pixelBounds = null;

        ContextMenu = new ContextMenu();
        ContextMenuOpening += OnContextMenuOpening;

        // The pet starts at a random spot. WPF places the window by Left and Top when it creates it, so they are set here.
        PetPlacement.Place(PickRandomSpot(), this);
    }

    /// <summary>
    /// Re-evaluates the pet's state right away and shows it. Call after anything that can change the state, such as a
    /// pomodoro command, instead of waiting for the next timer tick.
    /// </summary>
    public void RefreshState()
    {
        ApplyState(_brain.Tick());
    }

    /// <summary>
    /// Replaces the skin while the window is showing. A new skin is a new pet: whatever the old one was doing is over,
    /// and the new one starts on the ground at a random spot of a monitor picked at random.
    /// </summary>
    /// <param name="skin">
    /// The new skin.
    /// </param>
    /// <param name="frames">
    /// Decoded frames of the new skin.
    /// </param>
    public void SetSkin(Skin skin, SkinFrameCache frames)
    {
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(frames);

        _skin = skin;
        _frames = frames;

        if (_fall is not null)
        {
            EndFall(snapToLanding: false);
        }

        _hanging = false;
        _moveTimer.Stop();
        _brain.ResetState();
        _shownState = _brain.State;
        _facing = 1;
        ApplyFacing();

        _player = new AnimationPlayer(skin);
        _player.SetState(_shownState, CurrentLook());
        SpriteImage.ToolTip = Strings.StateHint(_shownState);

        ApplyFrameSize();
        PetPlacement.Place(PickRandomSpot(), this);

        // Settle on the monitor the window is on now, whose scaling the window knows once it is shown.
        SettleInWorkArea();

        ShowFrame();
        _animationWatch.Restart();
        _animationTimer.Interval = _player.Animation.FrameDuration;
    }

    /// <summary>
    /// Shows the pet at another size, keeping its bottom edge in place.
    /// </summary>
    /// <param name="size">
    /// The new size.
    /// </param>
    public void SetSize(PetSize size)
    {
        if (size == _size)
        {
            return;
        }

        _size = size;
        ApplyFrameSize();
        SettleInWorkArea();
        RepositionBubble();
    }

    /// <summary>
    /// Switches climbing along the edges of the screen on or off. Switching it off puts a crawling pet back on the
    /// ground.
    /// </summary>
    /// <param name="enabled">
    /// Whether the pet may climb along the edges.
    /// </param>
    public void SetClimbEdges(bool enabled)
    {
        _climbEdges = enabled;
        if (!enabled && IsCrawling)
        {
            EndCrawl();
        }
    }

    /// <summary>
    /// Resizes the window to the frame size of the current skin at the current size, keeping the bottom edge.
    /// </summary>
    private void ApplyFrameSize()
    {
        var bottom = Top + Height;
        Width = _size.Scale(_skin.FrameWidth);
        Height = _size.Scale(_skin.FrameHeight);
        Top = bottom - Height;
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
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!;
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

    /// <inheritdoc />
    double IPlacementTarget.ScaleX => VisualTreeHelper.GetDpi(this).DpiScaleX;

    /// <inheritdoc />
    double IPlacementTarget.ScaleY => VisualTreeHelper.GetDpi(this).DpiScaleY;

    /// <inheritdoc />
    void IPlacementTarget.MoveToPixels(int x, int y)
    {
        WindowMover.MoveToPixels(this, x, y);
    }

    /// <summary>
    /// Picks a random spot on the bottom edge of the work area of a monitor picked at random (above the taskbar), away
    /// from the left and right edges. The spot is worked out with the scaling of that monitor, so it is right once the
    /// window is there.
    /// </summary>
    /// <returns>
    /// The spot, in device pixels, with the scaling of the monitor it is on.
    /// </returns>
    private PetSpot PickRandomSpot()
    {
        var monitors = MonitorHelper.GetAllMonitors();
        if (monitors.Count == 0)
        {
            monitors = [new MonitorArea(MonitorHelper.GetWorkAreaPixels(this), VisualTreeHelper.GetDpi(this).DpiScaleX)];
        }

        return PetPlacement.PickOnGround(monitors, Width, Height, StartMargin, Random.Shared);
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

        // A crawl that ends high above the ground (an alert, sleep, letting go, switching climbing off) ends in a fall under
        // the umbrella, not in a jump to the ground. Being picked up is not an end; the drop decides.
        if (_shownState.EndsCrawlInFall(state) && TryStartFall())
        {
            ApplyState(_brain.State);
            return;
        }

        var previousState = _shownState;
        _shownState = state;

        // Grabbed again or otherwise interrupted in mid-air: the umbrella folds, and the fall is over.
        if (previousState == PetState.Fall && state != PetState.Fall)
        {
            EndFall(snapToLanding: false);
        }

        // Interrupted while crawling along the edges (an alert, sleep, hiding): the pet goes back to the ground. Being
        // picked up is not an interruption; the drop decides.
        if (previousState.IsCrawl() && !state.IsCrawl())
        {
            if (state != PetState.Drag)
            {
                _facing = 1;
                ApplyFacing();
                SettleInWorkArea();
            }
        }

        // Letting go of the edge: the pet falls back to where its mode lets it rest. Being picked up from the edge is
        // not letting go; the drop decides.
        if (_hanging && state != PetState.Hang)
        {
            _hanging = false;
            if (state != PetState.Drag)
            {
                _facing = 1;
                ApplyFacing();
                SettleInWorkArea();
            }
        }

        // The scene of a pet at its computer, and the scenes that carry a written label, are drawn facing right and
        // read wrongly mirrored. Walking sets the facing again, so nothing else needs to undo this. Hanging on to the
        // right edge needs the mirrored picture, and has no label.
        if (_facing != 1 && (state == PetState.Coding || (ScenarioCatalog.All.Contains(state) && state != PetState.Hang)))
        {
            _facing = 1;
            ApplyFacing();
        }

        // What the pet is doing, said in words when the mouse rests on it; nothing for plain states.
        SpriteImage.ToolTip = Strings.StateHint(state);

        if (_player.SetState(state, CurrentLook()))
        {
            ShowFrame();
        }

        _animationWatch.Restart();
        _animationTimer.Interval = _player.Animation.FrameDuration;

        if (state == PetState.Walk)
        {
            BeginWalk();
            StartMoveTimerIfVisible();
        }
        else if (state == PetState.Fall || state.IsCrawl())
        {
            StartMoveTimerIfVisible();
        }
        else
        {
            _moveTimer.Stop();
        }
    }

    /// <summary>
    /// Starts the walking timer, unless the pet is hidden; showing the pet again restarts it.
    /// </summary>
    private void StartMoveTimerIfVisible()
    {
        if (IsVisible)
        {
            _moveWatch.Restart();
            _moveTimer.Start();
        }
    }

    /// <summary>
    /// Shows or hides the window to match the brain's visibility. While hidden the animation and walking timers are
    /// stopped, so a hidden pet costs next to nothing; the brain timer keeps running because the pomodoro and the
    /// check for the full-screen application going away depend on it.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        if (_brain.Visibility.IsVisible)
        {
            if (!IsVisible)
            {
                Show();
                _animationWatch.Restart();
                _animationTimer.Start();
                UpdatePointerTimer();
                if (_shownState is PetState.Walk or PetState.Fall || _shownState.IsCrawl())
                {
                    StartMoveTimerIfVisible();
                }
            }
        }
        else if (IsVisible)
        {
            _animationTimer.Stop();
            _moveTimer.Stop();
            _pointerTimer.Stop();

            // A pet that is hidden mid-air lands at once, so it is not stuck falling while nobody sees it.
            if (_fall is not null)
            {
                EndFall(snapToLanding: true);
                _brain.OnFallEnded();
                ApplyState(_brain.State);
            }

            // A pet that is hidden while crawling goes back to the ground.
            if (IsCrawling)
            {
                _brain.OnCrawlEnded();
                Top = CreateMovementContext().Bounds.MaxY;
                ApplyState(_brain.State);
            }

            Hide();
        }
    }

    /// <summary>
    /// Mirrors the sprite so it faces the walking direction.
    /// </summary>
    private void ApplyFacing()
    {
        FlipTransform.ScaleX = _facing < 0 ? -1 : 1;
    }

    /// <summary>
    /// Describes where the pet is and where it may go, for the movement strategy.
    /// </summary>
    /// <returns>
    /// The context, using the cached work area.
    /// </returns>
    private MovementContext CreateMovementContext()
    {
        var bounds = new MovementBounds(
            _workArea.Left,
            _workArea.Right - Width,
            _workArea.Top,
            _workArea.Bottom - Height);
        return new MovementContext(
            new Point2(Left, Top),
            new Point2(Width, Height),
            bounds,
            ToPoint2(MonitorHelper.GetCursorPositionDip(this)),
            _options.WalkSpeed);
    }

    /// <summary>
    /// Converts a WPF point.
    /// </summary>
    /// <param name="point">
    /// The point to convert.
    /// </param>
    /// <returns>
    /// The same point as a <see cref="Point2"/>.
    /// </returns>
    private static Point2 ToPoint2(Point point)
    {
        return new Point2(point.X, point.Y);
    }

    /// <summary>
    /// Lets the movement strategy prepare a new walk and turns the sprite the way it will go.
    /// </summary>
    private void BeginWalk()
    {
        _workArea = MonitorHelper.GetWorkAreaDip(this);
        _moveTicksSinceWorkAreaRefresh = 0;
        _facing = _movement.Current.BeginWalk(CreateMovementContext(), _brain.WalkDirection);
        ApplyFacing();
    }

    /// <summary>
    /// Puts the pet where the current mode wants it after the mode changed: back on the taskbar line when docked, and
    /// into a fresh walk when it is mid-walk, so the new strategy takes over cleanly.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnMovementModeChanged(object? sender, EventArgs e)
    {
        if (_dragging)
        {
            return;
        }

        // A mode in which the pet rests wherever it is put has no ground to fall to: the pet stays in mid-air.
        if (_fall is not null && !_movement.Current.FallsWhenDropped)
        {
            _brain.OnFallEnded();
            ApplyState(_brain.State);
        }

        // A mode in which the pet does not walk along the taskbar has no edges to crawl along.
        if (IsCrawling && !CanCrawl)
        {
            EndCrawl();
        }

        SettleInWorkArea();
        if (_shownState == PetState.Walk)
        {
            BeginWalk();
        }
    }

    /// <summary>
    /// Tells where to look in the Idle animation: towards the pointer, as the sprite is drawn.
    /// </summary>
    /// <returns>
    /// The direction in the unmirrored drawing; <see cref="GazeDirection.None"/> unless the pet is idle.
    /// </returns>
    private GazeDirection CurrentLook()
    {
        if (_shownState != PetState.Idle)
        {
            return GazeDirection.None;
        }

        return FlipTransform.ScaleX < 0 ? GazeTracker.Mirror(_gaze) : _gaze;
    }

    /// <summary>
    /// Reads the mouse pointer: a wild shake makes the pet dizzy, and while it stands around its eyes follow the
    /// pointer.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnPointerTick(object? sender, EventArgs e)
    {
        if (_pressed || !_switches.IsEnabled)
        {
            SetGaze(GazeDirection.None);
            return;
        }

        var cursor = MonitorHelper.GetCursorPositionPixels();
        var reaction = _pointer.Read(
            _pointerWatch.Elapsed,
            cursor.X,
            cursor.Y,
            _switches.Allows(PetState.Dizzy) && _brain.State is not (PetState.Drag or PetState.Fall),
            _skin.CanLookAround && _shownState == PetState.Idle,
            _petBox);
        if (reaction.BecomeDizzy)
        {
            _brain.ActOut(PetState.Dizzy, DizzyDuration);
            ApplyState(_brain.State);
        }
        else if (reaction.Gaze is { } gaze)
        {
            SetGaze(gaze);
        }
    }

    /// <summary>
    /// Tells where the pet is on the screen, from the window rectangle that is read again only after a move.
    /// </summary>
    /// <returns>
    /// The centre and the width of the pet, in device pixels.
    /// </returns>
    private PetBox GetPetBox()
    {
        var bounds = _pixelBounds ??= MonitorHelper.GetWindowBoundsPixels(this);
        return new PetBox((bounds.Left + bounds.Right) / 2.0, (bounds.Top + bounds.Bottom) / 2.0, bounds.Width);
    }

    /// <summary>
    /// Turns the eyes, if they are not already looking that way.
    /// </summary>
    /// <param name="gaze">
    /// The direction on the screen.
    /// </param>
    private void SetGaze(GazeDirection gaze)
    {
        if (gaze == _gaze)
        {
            return;
        }

        _gaze = gaze;
        if (_player.SetState(_shownState, CurrentLook()))
        {
            ShowFrame();
            _animationTimer.Interval = _player.Animation.FrameDuration;
        }
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
        RefreshState();
        UpdatePointerTimer();
    }

    /// <summary>
    /// Runs the pointer timer only while the pet is on screen and something needs the pointer: shaking can make it
    /// dizzy, or its skin can look around. Checked every second, as the switches can change at any time.
    /// </summary>
    private void UpdatePointerTimer()
    {
        var wanted = IsVisible && _switches.IsEnabled && (_switches.Allows(PetState.Dizzy) || _skin.CanLookAround);
        if (wanted && !_pointerTimer.IsEnabled)
        {
            _pointerTimer.Start();
        }
        else if (!wanted && _pointerTimer.IsEnabled)
        {
            _pointerTimer.Stop();
            SetGaze(GazeDirection.None);
        }
    }

    /// <summary>
    /// Shows, updates or hides the speech bubble to match the brain's current message. The bubble stays hidden while
    /// the pet itself is hidden.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnMessageChanged(object? sender, EventArgs e)
    {
        var message = _brain.CurrentMessage;
        if (message is null || !IsVisible)
        {
            _bubble?.Hide();
            return;
        }

        if (_bubble is null)
        {
            _bubble = new BubbleWindow();
            _bubble.ActionInvoked += OnBubbleActionInvoked;
        }

        _bubble.SetContent(MessagePresenter.Present(message));
        _bubble.PositionAbove(this);
        if (!_bubble.IsVisible)
        {
            _bubble.Show();
        }
    }

    /// <summary>
    /// Carries out the action the user chose in the speech bubble.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="action">
    /// The chosen action.
    /// </param>
    private void OnBubbleActionInvoked(object? sender, MessageAction action)
    {
        _brain.Invoke(action);
        RefreshState();
    }

    /// <summary>
    /// Keeps the speech bubble attached to the pet while the pet moves.
    /// </summary>
    private void RepositionBubble()
    {
        if (_bubble is { IsVisible: true })
        {
            _bubble.PositionAbove(this);
        }
    }

    /// <summary>
    /// Fills the right-click menu with the pomodoro commands that apply right now, plus hiding the pet.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var items = ContextMenu.Items;
        PomodoroMenu.Populate(items, _brain, RefreshState);

        var openPanel = new MenuItem { Header = Strings.OpenPanel };
        openPanel.Click += (_, _) => _openPanel(null);
        items.Insert(0, openPanel);

        var settings = new MenuItem { Header = Strings.MenuSettings };
        settings.Click += (_, _) => _openPanel(PanelTab.Settings);
        items.Insert(1, settings);
        items.Insert(2, new Separator());

        if (_previewMenu)
        {
            items.Add(new Separator());
            var preview = new MenuItem { Header = Strings.MenuPreview };
            foreach (var state in Enum.GetValues<PetState>().Where(state => state is not (PetState.Drag or PetState.Sleep or PetState.Alert or PetState.Fall) && !state.IsCrawl()))
            {
                var item = new MenuItem { Header = Strings.StateName(state) };
                item.Click += (_, _) =>
                {
                    _brain.ActOut(state, PreviewDuration);
                    RefreshState();
                };
                preview.Items.Add(item);
            }

            items.Add(preview);
        }

        items.Add(new Separator());
        var hide = new MenuItem { Header = Strings.TrayHidePet };
        hide.Click += (_, _) => _brain.SetManuallyHidden(true);
        items.Add(hide);
    }

    /// <summary>
    /// Moves the pet one step as the movement strategy of the current mode directs.
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

        if (_shownState == PetState.Fall)
        {
            OnFallTick(delta);
            return;
        }

        if (IsCrawling)
        {
            OnCrawlTick(delta);
            return;
        }

        RefreshWorkAreaPeriodically();

        var context = CreateMovementContext();
        var step = _movement.Current.Step(context, delta);

        // One cheap move for both coordinates, and none at all when the step stays within the same device pixel.
        WindowMover.MoveTo(this, step.Position.X, step.Position.Y);

        if (step.Facing != _facing)
        {
            _facing = step.Facing;
            ApplyFacing();
        }

        if (step.Arrived)
        {
            _brain.OnReachedPointer();
            ApplyState(_brain.State);
        }
        else if (TryStartCrawlAtEnd(step.Position, context.Bounds))
        {
            ApplyState(_brain.State);
        }
    }

    /// <inheritdoc />
    bool IScreenEdges.HasMonitorBeside(bool left)
    {
        return MonitorHelper.HasMonitorBeside(this, left);
    }

    /// <inheritdoc />
    bool IScreenEdges.HasMonitorAbove()
    {
        return MonitorHelper.HasMonitorAbove(this);
    }

    /// <summary>
    /// Gets a value indicating whether the pet is crawling along the edges of the screen.
    /// </summary>
    private bool IsCrawling => _shownState.IsCrawl();

    /// <summary>
    /// Gets a value indicating whether the pet may crawl along the edges of the screen now: the user allows it, the pet
    /// walks along the taskbar, and the skin has the pictures for it.
    /// </summary>
    private bool CanCrawl =>
        _climbEdges
        && _movement.Current.FallsWhenDropped
        && _skin.Animations.ContainsKey(PetState.Climb)
        && _skin.Animations.ContainsKey(PetState.Ceiling);

    /// <summary>
    /// Lets a pet that has just walked into the left or right end of the screen start to climb it, by chance.
    /// </summary>
    /// <param name="position">
    /// Where the walk step put the pet.
    /// </param>
    /// <param name="bounds">
    /// The range the pet may take.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the pet now climbs; the state then needs showing.
    /// </returns>
    private bool TryStartCrawlAtEnd(Point2 position, MovementBounds bounds)
    {
        if (!CanCrawl || _dragging)
        {
            return false;
        }

        var start = CrawlStarter.AtWalkEnd(
            position,
            bounds,
            _options.ClimbChance,
            Random.Shared,
            this);
        if (start is not { } crawlStart)
        {
            return false;
        }

        StartCrawl(crawlStart);
        return true;
    }

    /// <summary>
    /// Starts to crawl along an edge of the screen.
    /// </summary>
    /// <param name="start">
    /// Where and how the pet starts.
    /// </param>
    private void StartCrawl(CrawlStart start)
    {
        _workArea = MonitorHelper.GetWorkAreaDip(this);
        _moveTicksSinceWorkAreaRefresh = 0;
        var bounds = CreateMovementContext().Bounds;

        if (start.Phase == CrawlPhase.Ceiling)
        {
            _facing = _crawl.StartOnCeiling(start.Direction);
            Left = bounds.Clamp(new Point2(Left, Top)).X;
            Top = bounds.MinY;
            _brain.OnCrawlStarted(PetState.Ceiling);
        }
        else
        {
            _facing = _crawl.StartOnWall(start.OnRightEdge, start.ClimbingUp);
            Left = start.OnRightEdge ? Math.Max(bounds.MinX, bounds.MaxX) : bounds.MinX;
            Top = start.Y;
            _brain.OnCrawlStarted(PetState.Climb);
        }

        ApplyFacing();
    }

    /// <summary>
    /// Moves the crawling pet one step, and ends the crawl when it reaches the ground or lets go.
    /// </summary>
    /// <param name="delta">
    /// The time since the previous step.
    /// </param>
    private void OnCrawlTick(TimeSpan delta)
    {
        if (!IsCrawling)
        {
            return;
        }

        RefreshWorkAreaPeriodically();

        var step = _crawl.Step(new Point2(Left, Top), CreateMovementContext().Bounds, delta);
        WindowMover.MoveTo(this, step.Position.X, step.Position.Y);

        if (step.Outcome != CrawlOutcome.Continue)
        {
            EndCrawl();
            return;
        }

        if (step.Facing != _facing)
        {
            _facing = step.Facing;
            ApplyFacing();
        }

        var state = step.Phase == CrawlPhase.Wall ? PetState.Climb : PetState.Ceiling;
        if (state != _shownState)
        {
            _brain.OnCrawlStarted(state);
            ApplyState(_brain.State);
        }
    }

    /// <summary>
    /// Ends the crawl along the edges of the screen. Showing the new state puts the pet back on the ground, with a fall
    /// under the umbrella if it is high enough above it.
    /// </summary>
    private void EndCrawl()
    {
        _brain.OnCrawlEnded();
        ApplyState(_brain.State);
    }

    /// <summary>
    /// The work area only changes when the taskbar or monitors change, so refresh it about once per second instead of
    /// on every movement tick.
    /// </summary>
    private void RefreshWorkAreaPeriodically()
    {
        if (_moveTicksSinceWorkAreaRefresh++ % WorkAreaRefreshEveryTicks == 0)
        {
            _workArea = MonitorHelper.GetWorkAreaDip(this);
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
        if (e.ClickCount >= 2)
        {
            _openPanel(null);
            return;
        }

        _pressed = true;
        _dragging = false;
        _pressPixel = MonitorHelper.GetCursorPositionPixels();
        _dragTracker.Begin(_pressPixel.X, _pressPixel.Y, MonitorHelper.GetWindowBoundsPixels(this));
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

        var cursor = MonitorHelper.GetCursorPositionPixels();
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var moved = Math.Sqrt(Math.Pow(cursor.X - _pressPixel.X, 2) + Math.Pow(cursor.Y - _pressPixel.Y, 2));
        if (!_dragging && moved >= DragThreshold * scale)
        {
            _dragging = true;
            _brain.OnDragStarted();
            ApplyState(_brain.State);
        }

        if (_dragging)
        {
            var bounds = MonitorHelper.GetWindowBoundsPixels(this);
            var (x, y) = _dragTracker.Move(cursor.X, cursor.Y, bounds.Width, bounds.Height);
            WindowMover.MoveToPixels(this, x, y);
        }
    }

    /// <summary>
    /// Ends a press: a drop settles the pet where its mode lets it rest, a short press counts as a click.
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
            if (!TryCrawlOnEdge() && !TryHangOnEdge() && !TryStartFall())
            {
                SettleInWorkArea();
            }
        }
        else
        {
            _brain.OnClicked();
        }

        ApplyState(_brain.State);
    }

    /// <summary>
    /// Lets a pet that was dropped at the top or at a side of the work area crawl along that edge, if it may.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the pet now crawls along an edge.
    /// </returns>
    private bool TryCrawlOnEdge()
    {
        if (!CanCrawl)
        {
            return false;
        }

        _workArea = MonitorHelper.GetWorkAreaDip(this);
        var start = CrawlStarter.OnDrop(
            new Point2(Left, Top),
            new Point2(Width, Height),
            CreateMovementContext().Bounds,
            EdgeSnapDistance,
            Random.Shared,
            this);
        if (start is not { } crawlStart)
        {
            return false;
        }

        StartCrawl(crawlStart);
        return true;
    }

    /// <summary>
    /// Lets the pet cling to the left or right edge of the work area if it was dropped close to it, and the skin has a
    /// way of showing that.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the pet now hangs on to an edge.
    /// </returns>
    private bool TryHangOnEdge()
    {
        if (!_skin.Animations.ContainsKey(PetState.Hang) || !_switches.Allows(PetState.Hang))
        {
            return false;
        }

        _workArea = MonitorHelper.GetWorkAreaDip(this);
        var bounds = CreateMovementContext().Bounds;
        var snapped = EdgeSnap.Find(
            new Point2(Left, Top),
            new Point2(Width, Height),
            bounds,
            EdgeSnapDistance,
            allowLeft: !MonitorHelper.HasMonitorBeside(this, left: true),
            allowRight: !MonitorHelper.HasMonitorBeside(this, left: false));
        if (snapped is not { } snap)
        {
            return false;
        }

        Left = snap.Position.X;
        Top = snap.Position.Y;
        _facing = snap.Facing;
        ApplyFacing();
        _hanging = true;
        _brain.ActOut(PetState.Hang, HangDuration);
        return true;
    }

    /// <summary>
    /// Lets a pet that was dropped well above where it would rest fall down to it.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the pet now falls; the timer then moves it until it lands.
    /// </returns>
    private bool TryStartFall()
    {
        if (!_movement.Current.FallsWhenDropped)
        {
            return false;
        }

        _workArea = MonitorHelper.GetWorkAreaDip(this);

        // The pet lands on the taskbar line, which is the bottom of the work area.
        var bounds = CreateMovementContext().Bounds;
        if (!FreeFall.ShouldFall(Top, bounds.MaxY, _options.FallMinHeight))
        {
            return false;
        }

        Left = bounds.Clamp(new Point2(Left, Top)).X;
        _fall = new FallState(Top, 0, false, false);
        _brain.OnFallStarted();
        return true;
    }

    /// <summary>
    /// Moves the falling pet one step, opens the umbrella or parachute once it is fast enough, and folds it away when
    /// the pet lands.
    /// </summary>
    /// <param name="elapsed">
    /// The time since the previous step.
    /// </param>
    private void OnFallTick(TimeSpan elapsed)
    {
        if (_fall is not { } current)
        {
            return;
        }

        RefreshWorkAreaPeriodically();

        // Worked out afresh on every step, so a change of pet size or work area in mid-air still lands on the ground.
        var bounds = CreateMovementContext().Bounds;
        var next = FreeFall.Step(current, bounds.MaxY, elapsed, _options);
        _fall = next;
        WindowMover.MoveTo(this, Math.Clamp(Left, bounds.MinX, Math.Max(bounds.MinX, bounds.MaxX)), next.Y);

        if (next.Opened && !current.Opened)
        {
            _umbrella ??= new UmbrellaWindow();
            _umbrella.Open(Random.Shared.Next(2) == 0 ? UmbrellaKind.Umbrella : UmbrellaKind.Parachute, this);
        }

        if (next.Landed)
        {
            _brain.OnFallEnded();
            ApplyState(_brain.State);
        }
    }

    /// <summary>
    /// Ends the fall in progress: the umbrella or parachute folds away.
    /// </summary>
    /// <param name="snapToLanding">
    /// Whether the pet is put on the ground at once; <see langword="false"/> leaves it where it is, as when the user
    /// grabs it in mid-air.
    /// </param>
    private void EndFall(bool snapToLanding)
    {
        if (snapToLanding && _fall is not null)
        {
            Top = CreateMovementContext().Bounds.MaxY;
        }

        _fall = null;
        _umbrella?.Fold();
    }

    /// <summary>
    /// Keeps the umbrella or parachute over the pet's head while the pet moves.
    /// </summary>
    private void RepositionUmbrella()
    {
        if (_umbrella is { IsVisible: true })
        {
            _umbrella.PositionAbove(this);
        }
    }

    /// <summary>
    /// Puts the pet where the current mode lets it rest, on the monitor it is on: on the taskbar line when docked,
    /// anywhere inside the work area when roaming.
    /// </summary>
    private void SettleInWorkArea()
    {
        _workArea = MonitorHelper.GetWorkAreaDip(this);

        // A pet in mid-air keeps falling to the ground (see OnFallTick); only keep it inside the work area sideways.
        if (_fall is not null)
        {
            Left = CreateMovementContext().Bounds.Clamp(new Point2(Left, Top)).X;
            return;
        }

        // A pet crawling along the edges stays where it is, inside the work area; the crawl puts it right again.
        if (IsCrawling)
        {
            var kept = CreateMovementContext().Bounds.Clamp(new Point2(Left, Top));
            Left = kept.X;
            Top = kept.Y;
            return;
        }

        var rest = _movement.Current.Settle(new Point2(Left, Top), CreateMovementContext().Bounds);
        Left = rest.X;
        Top = rest.Y;
    }
}
