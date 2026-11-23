using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskBuddy.App.Rendering;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Transparent, always-on-top window that renders the pet sprite, reacts to the mouse and walks along the bottom of
/// the work area.
/// </summary>
public partial class PetWindow : Window, IScreenEdges, IPlacementTarget
{
    /// <summary>
    /// How long the mouse must rest on the pet before it explains what it is doing, in milliseconds.
    /// </summary>
    private const int ToolTipDelayMilliseconds = 400;

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
    /// How often the pet's position is updated while walking, falling or crawling (20 steps a second). Movement is
    /// computed from the real time between ticks, so a longer interval makes the steps bigger, not the pet slower.
    /// Only runs while the pet moves. <see cref="WorkAreaRefreshEveryTicks"/> counts these ticks.
    /// </summary>
    private static readonly TimeSpan MoveInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// How often the pet's windows are lifted to the top of the topmost windows. Only a few windows can rise above the
    /// pet, so a few seconds is soon enough, and a lift that finds the pet on top costs next to nothing.
    /// </summary>
    private static readonly TimeSpan RaiseInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How often the brain re-reads sensors and re-evaluates the pet's state. The state machine counts time with the clock,
    /// not with ticks, so a longer interval only delays how soon the pet notices a change in what the user is doing; a few
    /// seconds is not noticed, and each tick costs a round of sensor queries on the UI thread.
    /// </summary>
    private static readonly TimeSpan BrainInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The pet being shown.
    /// </summary>
    private Pet _pet;

    /// <summary>
    /// How large the pet is shown.
    /// </summary>
    private PetSize _size;

    /// <summary>
    /// Plays the current animation of the pet.
    /// </summary>
    private AnimationPlayer _player;

    /// <summary>
    /// Which scenarios the user has switched on.
    /// </summary>
    private readonly ScenarioSwitches _switches;

    /// <summary>
    /// The window rectangle in device pixels, read once after each move; <see langword="null"/> when it must be read
    /// again.
    /// </summary>
    private PixelRect? _pixelBounds;

    /// <summary>
    /// Moves the pet along the sides and the top edge of the screen.
    /// </summary>
    private readonly EdgeCrawl _crawl;

    /// <summary>
    /// The umbrella or parachute the pet holds while it falls.
    /// </summary>
    private readonly UmbrellaFollower _umbrella;

    /// <summary>
    /// Decoded frames of the pet.
    /// </summary>
    private PetFrameCache _frames;

    /// <summary>
    /// Decides what the pet is doing.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Behavior tuning, used here for the walking speed.
    /// </summary>
    private readonly PetBehaviorOptions _options;

    /// <summary>
    /// Measures how fast the pet is moving at the end of a drag, so letting go of it in a flick throws it.
    /// </summary>
    private readonly ThrowTracker _throw;

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
    /// Timer that lifts the pet's windows to the top of the topmost ones; runs while the pet is on screen.
    /// </summary>
    private readonly DispatcherTimer _raiseTimer = new() { Interval = RaiseInterval };

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
    /// Measures the time since the current hop began.
    /// </summary>
    private readonly Stopwatch _hopWatch = new();

    /// <summary>
    /// The lift of the current hop, kept apart from the walk: the movement strategy is given the position on the line the
    /// pet walks along, never the raised one, so a hop never drifts the walk.
    /// </summary>
    private readonly HopLift _hop = new();

    /// <summary>
    /// Opens the panel window.
    /// </summary>
    private readonly Action<PanelTab?> _openPanel;

    /// <summary>
    /// Decides where the pet walks: along the taskbar line.
    /// </summary>
    private readonly DockedMovement _movement;

    /// <summary>
    /// Whether a change of state was held back because the pet lay across the seam between two monitors, and is to be
    /// shown once it is off the seam.
    /// </summary>
    private bool _stateHeld;

    /// <summary>
    /// Tells where the pet window is for the walk and crawl steps; see <see cref="CurrentPosition"/>.
    /// </summary>
    private readonly StepTracker _stepTracker = new();

    /// <summary>
    /// The real bounds of the pet window, read once during a movement tick and shared by everything that needs them in
    /// that tick; <see langword="null"/> outside a movement tick and after the window has been moved in it.
    /// </summary>
    private PixelRect? _tickBounds;

    /// <summary>
    /// The state currently shown, to detect changes.
    /// </summary>
    private PetState _shownState = PetState.Idle;

    /// <summary>
    /// Which way the sprite faces: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    private int _facing = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetWindow"/> class.
    /// </summary>
    /// <param name="pet">The pet that determines the window size and animations.</param>
    /// <param name="frames">Decoded frames for the pet.</param>
    /// <param name="brain">The brain that decides what the pet does.</param>
    /// <param name="options">Behavior tuning.</param>
    /// <param name="openPanel">Opens the panel window; called when the pet is right-clicked.</param>
    /// <param name="movement">Decides where the pet walks: along the taskbar line.</param>
    /// <param name="switches">Which scenarios the user has switched on.</param>
    /// <param name="size">How large the pet is shown at first.</param>
    public PetWindow(
        Pet pet,
        PetFrameCache frames,
        PetBrain brain,
        PetBehaviorOptions options,
        Action<PanelTab?> openPanel,
        DockedMovement movement,
        ScenarioSwitches switches,
        PetSize size)
    {
        // The tooltip of the pet reads its style from the styles of the thread this window lives on.
        PetThreadTheme.Apply(this);
        InitializeComponent();
        _umbrella = new UmbrellaFollower(this);

        _pet = pet;
        _size = size;
        _switches = switches;
        ToolTipService.SetInitialShowDelay(SpriteImage, ToolTipDelayMilliseconds);
        _player = new AnimationPlayer(pet);
        _frames = frames;
        _brain = brain;
        _brain.HopDuration = pet.HopDuration;
        _brain.CanHopNow = CanHopHere;
        _options = options;
        _throw = new ThrowTracker(options);
        _crawl = new EdgeCrawl(options, Random.Shared);
        _openPanel = openPanel;
        _movement = movement;

        Width = size.Scale(pet.FrameWidth);
        Height = size.Scale(pet.FrameHeight);
        ShowFrame();

        SourceInitialized += OnSourceInitialized;
        _animationTimer.Interval = _player.Animation.FrameDuration;
        _animationTimer.Tick += OnAnimationTick;
        _brainTimer.Tick += OnBrainTick;
        _raiseTimer.Tick += OnRaiseTick;
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
            _raiseTimer.Start();
            UpdatePointerTimer();

            PublishAutoSwitch();

            // The bubble is built when nothing else needs the thread, not at the first click.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, PrepareBubble);
        };
        Closed += (_, _) =>
        {
            _pointerTimer.Stop();
            _animationTimer.Stop();
            _brainTimer.Stop();
            _raiseTimer.Stop();
            _moveTimer.Stop();
            _brain.MessageChanged -= OnMessageChanged;
            _brain.VisibilityChanged -= OnVisibilityChanged;
            _bubble?.Close();
            _umbrella.Close();
        };

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonUp += OnMouseRightButtonUp;
        LostMouseCapture += OnLostMouseCapture;

        _brain.MessageChanged += OnMessageChanged;
        _brain.VisibilityChanged += OnVisibilityChanged;
        IsVisibleChanged += (_, _) => OnMessageChanged(this, EventArgs.Empty);
        LocationChanged += (_, _) =>
        {
            _pixelBounds = null;
            _tickBounds = null;
            RepositionBubble();
            _umbrella.OnPetLocationChanged();
        };
        SizeChanged += (_, _) =>
        {
            _pixelBounds = null;
            _tickBounds = null;
            _umbrella.OnPetResized();
        };
        DpiChanged += (_, _) =>
        {
            _pixelBounds = null;
            _tickBounds = null;
            _umbrella.OnPetResized();
        };

        // The pet starts at a random spot. WPF places the window by Left and Top when it creates it, so they are set here.
        PetPlacement.Place(PickRandomSpot(), this);
    }

    /// <summary>
    /// Replaces the pet while the window is showing. A new pet is a new pet: whatever the old one was doing is over,
    /// and the new one stands on the ground where the old one was (centered on it, as far as the work area allows) and
    /// arrives with an animation.
    /// </summary>
    /// <param name="pet">The new pet.</param>
    /// <param name="frames">Decoded frames of the new pet.</param>
    public void SetPet(Pet pet, PetFrameCache frames)
    {
        ArgumentNullException.ThrowIfNull(pet);
        ArgumentNullException.ThrowIfNull(frames);

        try
        {
            ReplacePet(pet, frames);
        }
        finally
        {
            // Also when the new pet could not be put in place, so that a pet that has left is never left invisible.
            PlayEnter();
        }
    }

    /// <summary>
    /// Puts the new pet in place of the old one; see <see cref="SetPet"/>.
    /// </summary>
    /// <param name="pet">The new pet.</param>
    /// <param name="frames">Decoded frames of the new pet.</param>
    private void ReplacePet(Pet pet, PetFrameCache frames)
    {
        var centerX = CurrentPosition().X + (Width / 2);

        // The new frames and the new player are put in place together, before anything that can pump messages (a hit test
        // reads the player's frame from the frames), so the two never belong to different pets.
        _pet = pet;
        _player = new AnimationPlayer(pet);
        _frames = frames;

        if (_fall is not null)
        {
            EndFall(snapToLanding: false);
        }

        _moveTimer.Stop();
        _brain.HopDuration = pet.HopDuration;

        // The window is placed afresh below, so a hop that was under way has nothing left to be raised from.
        _hop.Reset();
        _brain.ResetState();
        _shownState = _brain.State;
        _facing = 1;
        ApplyFacing();

        _player.SetState(_shownState, CurrentLook());
        SpriteImage.ToolTip = Strings.StateHint(_shownState);

        // The new pet may be of another size: keep the middle of it where the old pet's was. The bottom edge is kept by
        // ApplyFrameSize, and settling puts the pet on the ground and inside the work area (also one that was in the air or
        // on the edge of the screen).
        ApplyFrameSize();
        Left = centerX - (Width / 2);
        SettleInWorkArea();

        ShowFrame();
        _animationWatch.Restart();
        _animationTimer.Interval = _player.Animation.FrameDuration;
        UpdatePointerTimer();
    }

    /// <summary>
    /// Shows the pet at another size, keeping its bottom edge in place.
    /// </summary>
    /// <param name="size">The new size.</param>
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
    /// Resizes the window to the frame size of the current pet at the current size, keeping the bottom edge.
    /// </summary>
    private void ApplyFrameSize()
    {
        var bottom = Top + Height;
        Width = _size.Scale(_pet.FrameWidth);
        Height = _size.Scale(_pet.FrameHeight);
        Top = bottom - Height;
    }

    /// <summary>
    /// Configures the native window once its handle exists: transparent composition target, tool-window style,
    /// no DWM border, and the hit-test hook that makes transparent pixels click-through.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
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
    /// <param name="hwnd">Window handle.</param>
    /// <param name="msg">Window message.</param>
    /// <param name="wParam">Message <c>wParam</c>.</param>
    /// <param name="lParam">Message <c>lParam</c>.</param>
    /// <param name="handled">Set to <see langword="true"/> when the message was processed.</param>
    /// <returns>The hit-test result for <c>WM_NCHITTEST</c>; otherwise zero.</returns>
    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (WindowStyleHelper.IsScreenLayoutChange(msg, wParam))
        {
            // Let the window finish handling the change (a new scaling moves and resizes it) before placing the pet.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, OnScreenLayoutChanged);
            return 0;
        }

        if (msg != WindowStyleHelper.WmNcHitTest)
        {
            return 0;
        }

        if (_transitioning)
        {
            // A pet that is leaving or arriving cannot be grabbed; the clicks go to what is under it.
            handled = true;
            return WindowStyleHelper.HtTransparent;
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

            hit = _frames.GetMask(_player.CurrentFrameId).IsOpaqueAtNormalized(u, y / heightPx);
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
    /// Gets or sets what is told how long each timer tick took (which timer, how long), for the log of slow ticks.
    /// </summary>
    internal Action<string, TimeSpan>? TickTimed { get; set; }

    /// <summary>
    /// Says in a line what the pet window is doing, for the log of UI stalls. Call it on the UI thread.
    /// </summary>
    /// <returns>The state shown, what the brain wants, and which timers and movements are running.</returns>
    internal string Describe()
    {
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"shown={_shownState}, brain={_brain.State}, visible={IsVisible}, moveTimer={_moveTimer.IsEnabled}, "
            + $"pointerTimer={_pointerTimer.IsEnabled}, falling={_fall is not null}, crawling={IsCrawling}, "
            + $"umbrellaOpen={_umbrella.IsOpen}, bubble={_bubble is { IsVisible: true }}, pressed={_pressed}");
    }

    /// <summary>
    /// Tells <see cref="TickTimed"/> how long a tick took.
    /// </summary>
    /// <param name="tick">Which timer ticked.</param>
    /// <param name="startedAt">The <see cref="Stopwatch.GetTimestamp"/> from the start of the tick.</param>
    private void ReportTick(string tick, long startedAt)
    {
        TickTimed?.Invoke(tick, Stopwatch.GetElapsedTime(startedAt));
    }

    /// <summary>
    /// Shows the player's current frame in the sprite image.
    /// </summary>
    private void ShowFrame()
    {
        SpriteImage.Source = _frames.Get(_player.CurrentFrameId);
    }
}
