namespace DeskBuddy.Domain.Behavior;

/// <summary>
/// Decides which <see cref="PetState"/> the pet is in, based on user presence, direct interaction and elapsed time.
/// Pure logic: time comes from an injected <see cref="TimeProvider"/> and randomness from an injected
/// <see cref="Random"/>, so behavior is deterministic in tests.
/// </summary>
public sealed class PetBehaviorStateMachine
{
    /// <summary>
    /// The timings that drive transitions.
    /// </summary>
    private readonly PetBehaviorOptions _options;

    /// <summary>
    /// Source of the current time.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Source of randomness for dwell times, walk durations and walk direction.
    /// </summary>
    private readonly Random _random;

    /// <summary>
    /// The moment at which the current timed state (Idle, Walk, Happy) ends. Sleep, Drag, Fall, Tired, Alert and Coding
    /// last until their cause goes away, so they have no end time.
    /// </summary>
    private DateTimeOffset _stateEndsAt;

    /// <summary>
    /// Whether the current state is a moment (see <see cref="OnMoment"/>), held until <see cref="_stateEndsAt"/>.
    /// </summary>
    private bool _momentActive;

    /// <summary>
    /// The ambient state the machine is in because of <see cref="PetInputs.Ambient"/>, or <see langword="null"/> if
    /// it is not in one. Remembered so the pet gets up again once the ambient state goes away.
    /// </summary>
    private PetState? _ambientState;

    /// <summary>
    /// The moment at which the walking pet next hops.
    /// </summary>
    private DateTimeOffset _nextHopAt;

    /// <summary>
    /// The moment at which the walk ends that a hop interrupts, so the pet walks on until then afterwards.
    /// </summary>
    private DateTimeOffset _walkEndsAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetBehaviorStateMachine"/> class in the Idle state.
    /// </summary>
    /// <param name="options">
    /// The behavior timings.
    /// </param>
    /// <param name="timeProvider">
    /// The time source.
    /// </param>
    /// <param name="random">
    /// The random source.
    /// </param>
    public PetBehaviorStateMachine(PetBehaviorOptions options, TimeProvider timeProvider, Random random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(random);

        _options = options;
        _timeProvider = timeProvider;
        _random = random;

        Enter(PetState.Idle);
    }

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public PetState State { get; private set; }

    /// <summary>
    /// Gets or sets how long one hop lasts, which is how long the pet's own hop animation runs, or
    /// <see langword="null"/> if the pet has none, such as one that rolls along on a tread, and keeps to plain walking.
    /// A hop that would not be over before the walk ends is not made. The machine does not time the hop: it is held until
    /// <see cref="OnHopEnded"/>, which the pet window calls when the hop has been played out.
    /// </summary>
    public TimeSpan? HopDuration { get; set; }

    /// <summary>
    /// Gets or sets the question asked when a hop is about to start: whether the pet may hop here and now. The pet window
    /// answers <see langword="false"/> while the pet is close to the edge of the screen it walks towards, so a hop does
    /// not end against it; the hop is then given up and the time of the next one is picked at random again. Without it
    /// the pet always hops when its time has come.
    /// </summary>
    public Func<bool>? CanHopNow { get; set; }

    /// <summary>
    /// Gets the direction of the current or most recent walk: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    public int WalkDirection { get; private set; } = 1;

    /// <summary>
    /// Re-evaluates the state for the current time and the given inputs. Priority, highest first:
    /// Drag and Fall (which nothing interrupts), Alert, Sleep, moments (Happy and <see cref="OnMoment"/>), Ambient, Tired, Coding, then the
    /// Idle/Walk cycle.
    /// </summary>
    /// <param name="inputs">
    /// The current user presence, system condition and pending notices.
    /// </param>
    /// <returns>
    /// The state after the update.
    /// </returns>
    public PetState Update(PetInputs inputs)
    {
        // A real fall lasts until it is over; a glide that is only being acted out (previewed) ends like any moment.
        if (State == PetState.Drag || (State.IsFalling() && !_momentActive))
        {
            return State;
        }

        // An alert outranks sleep so that a user returning to the desk sees the pet already calling for attention.
        if (inputs.HasAlert)
        {
            if (State != PetState.Alert)
            {
                Enter(PetState.Alert);
            }

            return State;
        }

        if (State == PetState.Alert)
        {
            Enter(PetState.Idle);
        }

        if (inputs.UserIdle >= _options.SleepAfter)
        {
            if (State != PetState.Sleep)
            {
                Enter(PetState.Sleep);
            }

            return State;
        }

        if (State == PetState.Sleep)
        {
            Enter(PetState.Idle);
            return State;
        }

        // Crawling along the edges goes on until the window says it is over; only the above can cut it short.
        if (State.IsCrawl())
        {
            return State;
        }

        // A hop is played out to its end, whenever that is; only the above can cut it short.
        if (State == PetState.Hop)
        {
            return State;
        }

        var now = _timeProvider.GetUtcNow();
        if ((State == PetState.Happy || _momentActive) && now < _stateEndsAt)
        {
            return State;
        }

        if (inputs.Ambient is { } ambient)
        {
            if (State != ambient)
            {
                Enter(ambient);
            }

            _ambientState = ambient;
            return State;
        }

        if (_ambientState is { } previousAmbient)
        {
            _ambientState = null;
            if (State == previousAmbient)
            {
                Enter(PetState.Idle);
                return State;
            }
        }

        if (inputs.IsSystemStrained)
        {
            if (State != PetState.Tired)
            {
                Enter(PetState.Tired);
            }

            return State;
        }

        if (State == PetState.Tired)
        {
            Enter(PetState.Idle);
            return State;
        }

        if (inputs.IsDeveloping)
        {
            if (State != PetState.Coding)
            {
                Enter(PetState.Coding);
            }

            return State;
        }

        if (State == PetState.Coding)
        {
            Enter(PetState.Idle);
            return State;
        }

        if (HopDuration is { } hopDuration && State == PetState.Walk && now >= _nextHopAt && _stateEndsAt - now > hopDuration)
        {
            if (CanHopNow?.Invoke() ?? true)
            {
                _walkEndsAt = _stateEndsAt;
                State = PetState.Hop;
                _stateEndsAt = DateTimeOffset.MaxValue;
                return State;
            }

            // No room to hop here: try again at another random time.
            ScheduleHop(now);
        }

        if (now >= _stateEndsAt)
        {
            Enter(State == PetState.Idle ? PetState.Walk : PetState.Idle);
        }

        return State;
    }

    /// <summary>
    /// Notifies the machine that the user started dragging the pet.
    /// </summary>
    public void OnDragStarted()
    {
        Enter(PetState.Drag);
    }

    /// <summary>
    /// Notifies the machine that the user dropped the pet. Ignored if the pet is not being dragged.
    /// </summary>
    public void OnDragEnded()
    {
        if (State == PetState.Drag)
        {
            Enter(PetState.Idle);
        }
    }

    /// <summary>
    /// Notifies the machine that the pet was dropped from a height and started to fall. The state is held until
    /// <see cref="OnFallEnded"/>; grabbing the pet again (<see cref="OnDragStarted"/>) interrupts it.
    /// </summary>
    public void OnFallStarted()
    {
        Enter(PetState.Fall);
    }

    /// <summary>
    /// Notifies the machine that the falling pet has opened its umbrella or parachute. Ignored if the pet is not falling.
    /// </summary>
    public void OnFallOpened()
    {
        if (State == PetState.Fall)
        {
            Enter(PetState.Glide);
        }
    }

    /// <summary>
    /// Notifies the machine that the falling pet has landed. Ignored if the pet is not falling.
    /// </summary>
    public void OnFallEnded()
    {
        if (State.IsFalling())
        {
            Enter(PetState.Idle);
        }
    }

    /// <summary>
    /// Notifies the machine that the pet started to crawl along an edge of the screen. The state is held until
    /// <see cref="OnCrawlEnded"/>; grabbing the pet (<see cref="OnDragStarted"/>) interrupts it. Ignored while the pet
    /// is being dragged or is falling.
    /// </summary>
    /// <param name="state">
    /// <see cref="PetState.Climb"/> or <see cref="PetState.ClimbDown"/> on a side of the screen, <see cref="PetState.Ceiling"/> on
    /// the top edge.
    /// </param>
    public void OnCrawlStarted(PetState state)
    {
        if (!state.IsCrawl() || State == PetState.Drag || State.IsFalling())
        {
            return;
        }

        Enter(state);
    }

    /// <summary>
    /// Notifies the machine that the pet stopped crawling along the edges. Ignored if the pet is not crawling.
    /// </summary>
    public void OnCrawlEnded()
    {
        if (State.IsCrawl())
        {
            Enter(PetState.Idle);
        }
    }

    /// <summary>
    /// Notifies the machine that the pet has played its hop to the end. The pet walks on in the same direction for what is
    /// left of the walk, or stands if the walk is over. Ignored if the pet is not hopping.
    /// </summary>
    public void OnHopEnded()
    {
        if (State != PetState.Hop)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        if (now >= _walkEndsAt)
        {
            Enter(PetState.Idle);
            return;
        }

        State = PetState.Walk;
        _stateEndsAt = _walkEndsAt;
        ScheduleHop(now);
    }

    /// <summary>
    /// Puts the pet back to standing, whatever it was doing: used when it is replaced by a new one.
    /// </summary>
    public void Reset()
    {
        Enter(PetState.Idle);
    }

    /// <summary>
    /// Notifies the machine that the user clicked the pet. Ignored while the pet is being dragged or is falling.
    /// </summary>
    public void OnClicked()
    {
        OnDelighted();
    }

    /// <summary>
    /// Notifies the machine that something pleased the pet (a click, or reaching the mouse pointer it was chasing).
    /// Ignored while the pet is being dragged or is falling.
    /// </summary>
    public void OnDelighted()
    {
        if (State != PetState.Drag && !State.IsFalling() && !State.IsCrawl())
        {
            Enter(PetState.Happy);
        }
    }

    /// <summary>
    /// Notifies the machine that something short and one-off happened that the pet should act out for a while, such as
    /// a finished build or the user coming back. The state is held for the given time, then the pet carries on as
    /// usual. Ignored while the pet is being dragged or is falling; an alert, or the user being away, still takes over.
    /// </summary>
    /// <param name="state">
    /// The state to act out.
    /// </param>
    /// <param name="duration">
    /// How long to stay in it.
    /// </param>
    public void OnMoment(PetState state, TimeSpan duration)
    {
        if (State == PetState.Drag || State.IsFalling() || State.IsCrawl())
        {
            return;
        }

        Enter(state);
        _stateEndsAt = _timeProvider.GetUtcNow() + duration;
        _momentActive = true;
    }

    /// <summary>
    /// Switches to a state and schedules its end.
    /// </summary>
    /// <param name="state">
    /// The state to enter.
    /// </param>
    private void Enter(PetState state)
    {
        State = state;
        _momentActive = false;

        var now = _timeProvider.GetUtcNow();
        _stateEndsAt = state switch
        {
            PetState.Idle => now + _random.NextTimeSpan(_options.IdleDwellMin, _options.IdleDwellMax),
            PetState.Walk => now + _random.NextTimeSpan(_options.WalkDurationMin, _options.WalkDurationMax),
            PetState.Happy => now + _options.HappyDuration,
            _ => DateTimeOffset.MaxValue,
        };

        if (state == PetState.Walk)
        {
            WalkDirection = _random.Next(2) == 0 ? -1 : 1;
            ScheduleHop(now);
        }
    }

    /// <summary>
    /// Picks the moment at which the walking pet next hops.
    /// </summary>
    /// <param name="now">
    /// The current time.
    /// </param>
    private void ScheduleHop(DateTimeOffset now)
    {
        _nextHopAt = now + _random.NextTimeSpan(_options.HopIntervalMin, _options.HopIntervalMax);
    }
}
