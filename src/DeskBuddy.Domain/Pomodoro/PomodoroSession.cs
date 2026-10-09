namespace DeskBuddy.Domain.Pomodoro;

/// <summary>
/// A pomodoro timer: work, then a short break (a long one every few rounds). A work phase flows straight into its
/// break; when a break ends the session stops and waits for the user to start the next round.
/// Phase ends are tracked as wall-clock moments rather than accumulated ticks, so the timer stays correct across
/// sleep and hibernation.
/// </summary>
public sealed class PomodoroSession
{
    /// <summary>
    /// The durations of the cycle.
    /// </summary>
    private readonly PomodoroOptions _options;

    /// <summary>
    /// Source of the current time.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The moment the current phase ends; meaningful only while a phase runs and the session is not paused.
    /// </summary>
    private DateTimeOffset _phaseEndsAt;

    /// <summary>
    /// Time left in the current phase at the moment it was paused.
    /// </summary>
    private TimeSpan _remainingWhenPaused;

    /// <summary>
    /// Initializes a new instance of the <see cref="PomodoroSession"/> class in the stopped state.
    /// </summary>
    /// <param name="options">
    /// The cycle durations.
    /// </param>
    /// <param name="timeProvider">
    /// The time source.
    /// </param>
    public PomodoroSession(PomodoroOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the current phase.
    /// </summary>
    public PomodoroPhase Phase { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the current phase is paused.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Gets how many work phases have been completed in the current cycle (reset after a long break or on stop).
    /// </summary>
    public int CompletedWorkSessions { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a phase is actively counting down.
    /// </summary>
    public bool IsRunning => Phase != PomodoroPhase.Stopped && !IsPaused;

    /// <summary>
    /// Gets the time left in the current phase; <see cref="TimeSpan.Zero"/> when stopped.
    /// </summary>
    public TimeSpan Remaining
    {
        get
        {
            if (Phase == PomodoroPhase.Stopped)
            {
                return TimeSpan.Zero;
            }

            return IsPaused ? _remainingWhenPaused : TimeSpan.FromTicks(Math.Max(0, (_phaseEndsAt - _timeProvider.GetUtcNow()).Ticks));
        }
    }

    /// <summary>
    /// Starts a work phase. Ignored unless the session is stopped.
    /// </summary>
    public void Start()
    {
        if (Phase == PomodoroPhase.Stopped)
        {
            Enter(PomodoroPhase.Work);
        }
    }

    /// <summary>
    /// Pauses the current phase. Ignored if nothing is running.
    /// </summary>
    public void Pause()
    {
        if (!IsRunning)
        {
            return;
        }

        _remainingWhenPaused = Remaining;
        IsPaused = true;
    }

    /// <summary>
    /// Resumes a paused phase. Ignored if not paused.
    /// </summary>
    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        _phaseEndsAt = _timeProvider.GetUtcNow() + _remainingWhenPaused;
        IsPaused = false;
    }

    /// <summary>
    /// Ends the current phase early. Skipping work counts it as completed and starts the break; skipping a break
    /// stops the session. Ignored when stopped.
    /// </summary>
    public void Skip()
    {
        switch (Phase)
        {
            case PomodoroPhase.Work:
                CompleteWork();
                break;
            case PomodoroPhase.ShortBreak:
            case PomodoroPhase.LongBreak:
                CompleteBreak();
                break;
        }
    }

    /// <summary>
    /// Stops the session and forgets the progress of the current cycle.
    /// </summary>
    public void Stop()
    {
        Phase = PomodoroPhase.Stopped;
        IsPaused = false;
        CompletedWorkSessions = 0;
    }

    /// <summary>
    /// Advances the session if the current phase has run out. Call periodically.
    /// </summary>
    /// <returns>
    /// The phase boundary that was crossed, or <see cref="PomodoroTransition.NoChange"/>.
    /// </returns>
    public PomodoroTransition Update()
    {
        if (!IsRunning)
        {
            return PomodoroTransition.NoChange;
        }

        var now = _timeProvider.GetUtcNow();
        if (now < _phaseEndsAt)
        {
            return PomodoroTransition.NoChange;
        }

        var isStale = now - _phaseEndsAt > _options.StaleAfter;
        if (Phase == PomodoroPhase.Work)
        {
            CompleteWork(isStale);
            return new PomodoroTransition(PomodoroTransitionKind.WorkCompleted, isStale);
        }

        CompleteBreak();
        return new PomodoroTransition(PomodoroTransitionKind.BreakCompleted, isStale);
    }

    /// <summary>
    /// Counts the finished work phase and moves on to the matching break, or stops if the completion is stale.
    /// </summary>
    /// <param name="stop">
    /// If <see langword="true"/>, stop instead of starting the break.
    /// </param>
    private void CompleteWork(bool stop = false)
    {
        CompletedWorkSessions++;
        if (stop)
        {
            Phase = PomodoroPhase.Stopped;
            IsPaused = false;
            return;
        }

        var isLongBreak = CompletedWorkSessions % _options.LongBreakEvery == 0;
        Enter(isLongBreak ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak);
    }

    /// <summary>
    /// Finishes a break: the session stops, and a finished long break also closes the cycle.
    /// </summary>
    private void CompleteBreak()
    {
        if (Phase == PomodoroPhase.LongBreak)
        {
            CompletedWorkSessions = 0;
        }

        Phase = PomodoroPhase.Stopped;
        IsPaused = false;
    }

    /// <summary>
    /// Begins a phase now.
    /// </summary>
    /// <param name="phase">
    /// The phase to begin.
    /// </param>
    private void Enter(PomodoroPhase phase)
    {
        Phase = phase;
        IsPaused = false;

        var duration = phase switch
        {
            PomodoroPhase.Work => _options.WorkDuration,
            PomodoroPhase.ShortBreak => _options.ShortBreakDuration,
            _ => _options.LongBreakDuration,
        };
        _phaseEndsAt = _timeProvider.GetUtcNow() + duration;
    }
}
