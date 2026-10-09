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
    /// The moment at which the current timed state (Idle, Walk, Happy) ends. Sleep, Drag, Tired, Alert and Focus last
    /// until their cause goes away, so they have no end time.
    /// </summary>
    private DateTimeOffset _stateEndsAt;

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
    /// Gets the direction of the current or most recent walk: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    public int WalkDirection { get; private set; } = 1;

    /// <summary>
    /// Re-evaluates the state for the current time and the given inputs. Priority, highest first:
    /// Drag, Alert, Sleep, Happy, Tired, Focus, then the Idle/Walk cycle.
    /// </summary>
    /// <param name="inputs">
    /// The current user presence, system condition and pending notices.
    /// </param>
    /// <returns>
    /// The state after the update.
    /// </returns>
    public PetState Update(PetInputs inputs)
    {
        if (State == PetState.Drag)
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

        var now = _timeProvider.GetUtcNow();
        if (State == PetState.Happy && now < _stateEndsAt)
        {
            return State;
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

        if (inputs.IsFocusing)
        {
            if (State != PetState.Focus)
            {
                Enter(PetState.Focus);
            }

            return State;
        }

        if (State == PetState.Focus)
        {
            Enter(PetState.Idle);
            return State;
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
    /// Notifies the machine that the user clicked the pet. Ignored while the pet is being dragged.
    /// </summary>
    public void OnClicked()
    {
        OnDelighted();
    }

    /// <summary>
    /// Notifies the machine that something pleased the pet (a click, or reaching the mouse pointer it was chasing).
    /// Ignored while the pet is being dragged.
    /// </summary>
    public void OnDelighted()
    {
        if (State != PetState.Drag)
        {
            Enter(PetState.Happy);
        }
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

        var now = _timeProvider.GetUtcNow();
        _stateEndsAt = state switch
        {
            PetState.Idle => now + RandomBetween(_options.IdleDwellMin, _options.IdleDwellMax),
            PetState.Walk => now + RandomBetween(_options.WalkDurationMin, _options.WalkDurationMax),
            PetState.Happy => now + _options.HappyDuration,
            _ => DateTimeOffset.MaxValue,
        };

        if (state == PetState.Walk)
        {
            WalkDirection = _random.Next(2) == 0 ? -1 : 1;
        }
    }

    /// <summary>
    /// Picks a random duration between two bounds.
    /// </summary>
    /// <param name="min">
    /// Lower bound.
    /// </param>
    /// <param name="max">
    /// Upper bound; if not greater than <paramref name="min"/>, <paramref name="min"/> is returned.
    /// </param>
    /// <returns>
    /// A duration in the range <c>[min, max)</c>.
    /// </returns>
    private TimeSpan RandomBetween(TimeSpan min, TimeSpan max)
    {
        return max <= min ? min : min + ((max - min) * _random.NextDouble());
    }
}
