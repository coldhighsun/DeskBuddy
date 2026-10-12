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
    /// Well below <see cref="StartupMargin"/>, so a pet nudged from its starting place does not count as dropped at the
    /// edge.
    /// </summary>
    private const double EdgeSnapDistance = 8;

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
        };
        SizeChanged += (_, _) => _pixelBounds = null;
        DpiChanged += (_, _) => _pixelBounds = null;

        ContextMenu = new ContextMenu();
        ContextMenuOpening += OnContextMenuOpening;

        PlaceAtBottomRight();
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
    /// Replaces the skin while the window is showing. The pet keeps its feet where they were, so a skin of a different
    /// size grows or shrinks upward, and is then kept inside the work area.
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
        _player = new AnimationPlayer(skin);
        _player.SetState(_shownState, CurrentLook());
        _frames = frames;

        ApplyFrameSize();

        ShowFrame();
        _animationWatch.Restart();
        _animationTimer.Interval = _player.Animation.FrameDuration;
        SettleInWorkArea();
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
                if (_shownState == PetState.Walk)
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
            _switches.Allows(PetState.Dizzy) && _brain.State != PetState.Drag,
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
            foreach (var state in Enum.GetValues<PetState>().Where(state => state is not (PetState.Drag or PetState.Sleep or PetState.Alert)))
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

        // The work area only changes when the taskbar or monitors change, so refresh it about once per second
        // instead of on every movement tick.
        if (_moveTicksSinceWorkAreaRefresh++ % WorkAreaRefreshEveryTicks == 0)
        {
            _workArea = MonitorHelper.GetWorkAreaDip(this);
        }

        var step = _movement.Current.Step(CreateMovementContext(), delta);

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
            if (!TryHangOnEdge())
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
    /// Puts the pet where the current mode lets it rest, on the monitor it is on: on the taskbar line when docked,
    /// anywhere inside the work area when roaming.
    /// </summary>
    private void SettleInWorkArea()
    {
        _workArea = MonitorHelper.GetWorkAreaDip(this);
        var rest = _movement.Current.Settle(new Point2(Left, Top), CreateMovementContext().Bounds);
        Left = rest.X;
        Top = rest.Y;
    }
}
