using System.Windows.Threading;

namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Watches the UI thread from a thread of its own and logs when it stops answering, so that a freeze leaves a trace in the
/// log: nothing the UI thread does can be logged while it is stuck, and a freeze that ends in the user quitting leaves no
/// crash to look at. A probe is posted at input priority every <see cref="ProbeInterval"/>; input priority is what
/// separates a thread that is busy from one whose pending work (the animation timers run above input) keeps the user's
/// clicks waiting.
/// </summary>
public sealed class UiStallMonitor : IDisposable
{
    /// <summary>
    /// How often a probe is posted.
    /// </summary>
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// How long a probe may go unanswered before the stall is logged. Short enough to catch what the user feels as a hitch.
    /// </summary>
    private static readonly TimeSpan StallThreshold = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// The UI thread's dispatcher, which the probes are posted to.
    /// </summary>
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Where the stalls are logged.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Describes what the application is doing; called on the UI thread, whenever a probe is answered.
    /// </summary>
    private readonly Func<string> _describe;

    /// <summary>
    /// Counts probes and decides when a stall is to be reported.
    /// </summary>
    private readonly StallTracker _tracker = new(StallThreshold);

    /// <summary>
    /// Wakes the watchdog thread up to leave when the monitor is disposed.
    /// </summary>
    private readonly ManualResetEventSlim _stopping = new();

    /// <summary>
    /// What the application was doing when the UI thread last answered a probe; the best account of what it was doing just
    /// before it stalled, as the thread cannot be asked while it is stuck.
    /// </summary>
    private volatile string _lastKnown = "not known yet";

    /// <summary>
    /// This process, kept so that taking a sample does not look it up again.
    /// </summary>
    private readonly System.Diagnostics.Process _self = System.Diagnostics.Process.GetCurrentProcess();

    /// <summary>
    /// How the process and the machine were when the outstanding probe was posted; what a stall is measured against.
    /// </summary>
    private volatile PressureSample? _probeStart;

    /// <summary>
    /// The watchdog thread, or <see langword="null"/> before <see cref="Start"/>.
    /// </summary>
    private Thread? _thread;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiStallMonitor"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher of the UI thread to watch.</param>
    /// <param name="logger">Where the stalls are logged.</param>
    /// <param name="time">The clock.</param>
    /// <param name="describe">
    /// Describes what the application is doing, for the log; it is called on the UI thread.
    /// </param>
    public UiStallMonitor(Dispatcher dispatcher, ILogger logger, TimeProvider time, Func<string> describe)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _time = time;
        _describe = describe;
    }

    /// <summary>
    /// Starts watching.
    /// </summary>
    public void Start()
    {
        _thread = new Thread(Watch) { IsBackground = true, Name = "UI stall monitor" };
        _thread.Start();
    }

    /// <summary>
    /// Stops watching.
    /// </summary>
    public void Dispose()
    {
        _stopping.Set();
        _thread?.Join(TimeSpan.FromSeconds(1));
        _stopping.Dispose();
        _self.Dispose();
    }

    /// <summary>
    /// The watchdog thread: posts probes and reports the one that is not answered.
    /// </summary>
    private void Watch()
    {
        while (!_stopping.Wait(ProbeInterval))
        {
            var now = _time.GetUtcNow();
            if (_tracker.TryBeginProbe(now))
            {
                _probeStart = PressureSample.Take(_self, now);
                _dispatcher.BeginInvoke(DispatcherPriority.Input, OnProbeAnswered);
            }

            if (_tracker.CheckStalled(now) is { } waited)
            {
                _logger.LogWarning(
                    "The UI thread has not answered for {WaitedMs:F0} ms. What it was doing when it last answered: {LastKnown}. Since the probe: {Pressure}",
                    waited.TotalMilliseconds,
                    _lastKnown,
                    DescribePressure(now));
            }
        }
    }

    /// <summary>
    /// Runs on the UI thread when it gets to a probe.
    /// </summary>
    private void OnProbeAnswered()
    {
        var state = _describe();
        _lastKnown = state;

        var now = _time.GetUtcNow();
        if (_tracker.Answer(now) is { } waited)
        {
            _logger.LogWarning(
                "The UI thread answered again after {WaitedMs:F0} ms. What it is doing now: {State}. Since the probe: {Pressure}",
                waited.TotalMilliseconds,
                state,
                DescribePressure(now));
        }
    }

    /// <summary>
    /// Describes what the process and the machine did since the probe that is outstanding was posted.
    /// </summary>
    /// <param name="now">The time.</param>
    /// <returns>The line for the log; see <see cref="PressureSample.Describe"/>.</returns>
    private string DescribePressure(DateTimeOffset now)
    {
        var start = _probeStart;
        return start is null ? "no sample" : PressureSample.Describe(start, PressureSample.Take(_self, now));
    }
}
