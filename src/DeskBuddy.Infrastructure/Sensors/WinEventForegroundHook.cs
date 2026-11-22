using System.Runtime.CompilerServices;

namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Tells which program is in front each time Windows brings another window to the front, with a WinEvent hook
/// (<c>EVENT_SYSTEM_FOREGROUND</c>), instead of asking every half second. The hook lives on a thread of its own with a
/// message loop, so it neither needs the UI thread nor waits for it, and the lookup of the program's name, a system call
/// that can wait for another program, never runs where the UI runs. Only the name of the program is read, never the
/// window title. There is one hook per process.
/// </summary>
public sealed unsafe partial class WinEventForegroundHook : IDisposable
{
    /// <summary>
    /// <c>EVENT_SYSTEM_FOREGROUND</c>: another window became the foreground window.
    /// </summary>
    private const uint EventSystemForeground = 0x0003;

    /// <summary>
    /// <c>WINEVENT_OUTOFCONTEXT</c>: the callback runs on the hook's own thread, and nothing is loaded into other programs.
    /// </summary>
    private const uint WinEventOutOfContext = 0x0000;

    /// <summary>
    /// <c>OBJID_WINDOW</c>: the event is about the window itself.
    /// </summary>
    private const int ObjectIdWindow = 0;

    /// <summary>
    /// <c>WM_QUIT</c>: ends the message loop.
    /// </summary>
    private const uint WmQuit = 0x0012;

    /// <summary>
    /// How long to wait for the hook thread to set up, or to end, before going on without it.
    /// </summary>
    private static readonly TimeSpan ThreadWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The hook that receives the events; the callback of the system has no state of its own.
    /// </summary>
    private static WinEventForegroundHook? s_active;

    /// <summary>
    /// Where the name of the program in front is passed on to.
    /// </summary>
    private readonly Action<string?> _publish;

    /// <summary>
    /// Where setup failures are logged.
    /// </summary>
    private readonly ILogger<WinEventForegroundHook> _logger;

    /// <summary>
    /// Set once the hook thread has tried to install the hook, whether it worked or not.
    /// </summary>
    private readonly ManualResetEventSlim _ready = new();

    /// <summary>
    /// The thread that owns the hook and its message loop.
    /// </summary>
    private readonly Thread _thread;

    /// <summary>
    /// Guards <see cref="_threadId"/> and <see cref="_stopRequested"/>, which the hook thread and the thread that disposes
    /// the hook set and read in opposite order, so that one of the two always sees the other.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// The identifier of the hook thread once its message queue exists, for the message that ends its loop; zero before.
    /// </summary>
    private uint _threadId;

    /// <summary>
    /// Whether <see cref="Dispose"/> was called; a hook thread that is not yet in its loop then never enters it.
    /// </summary>
    private bool _stopRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinEventForegroundHook"/> class and starts the hook. Returns once the
    /// hook is installed, or has failed, or took too long; the first publish follows on the hook's thread.
    /// </summary>
    /// <param name="publish">
    /// Called with the name of the program in front without <c>.exe</c> (or <see langword="null"/> if it cannot be
    /// read), soon after the start and then on every change; always from the hook's own thread.
    /// </param>
    /// <param name="logger">Where a hook that could not be installed is logged.</param>
    public WinEventForegroundHook(Action<string?> publish, ILogger<WinEventForegroundHook> logger)
    {
        _publish = publish;
        _logger = logger;
        _thread = new Thread(Run) { IsBackground = true, Name = "Foreground window hook" };
        _thread.Start();
        _ready.Wait(ThreadWait);
    }

    /// <summary>
    /// Gets a value indicating whether the hook is installed, so that every change of the foreground window is passed on.
    /// When it is not, nothing will be published and the caller must ask by another way.
    /// </summary>
    public bool IsInstalled { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the system's events reach this hook. Only one hook does; a hook made later takes
    /// over, and the one before it stops being the one that is told.
    /// </summary>
    public bool IsActive => ReferenceEquals(Volatile.Read(ref s_active), this);

    /// <summary>
    /// Removes the hook and ends its thread.
    /// </summary>
    public void Dispose()
    {
        // Either the thread has not yet announced its queue, and then it sees the request before it starts its loop, or it
        // has, and then the quit message reaches it.
        uint threadId;
        lock (_gate)
        {
            _stopRequested = true;
            threadId = _threadId;
        }

        if (threadId != 0)
        {
            _ = PostThreadMessage(threadId, WmQuit, 0, 0);
        }

        // The event is only released once the thread is gone: a thread that is slow to end still sets it, and setting a
        // disposed event throws on a thread nobody catches.
        if (!_thread.IsAlive || _thread.Join(ThreadWait))
        {
            _ready.Dispose();
        }
    }

    /// <summary>
    /// Receives an event from the system on the hook's thread.
    /// </summary>
    /// <param name="hook">The hook handle.</param>
    /// <param name="eventType">Which event happened.</param>
    /// <param name="window">The window the event is about.</param>
    /// <param name="objectId">What in the window the event is about.</param>
    /// <param name="childId">Which child of that object.</param>
    /// <param name="eventThread">The thread that caused the event.</param>
    /// <param name="eventTime">When the event happened.</param>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnWinEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint eventThread, uint eventTime)
    {
        if (eventType == EventSystemForeground && objectId == ObjectIdWindow && window != 0)
        {
            s_active?.Publish(window);
        }
    }

    /// <summary>
    /// Looks up the program of a window that came to the front and passes its name on. Never throws: it runs in a callback
    /// of the system and on a thread nobody catches for, and the listeners behind <c>publish</c> are not ours to trust.
    /// </summary>
    /// <param name="window">The window that is now in front, or zero if there is none.</param>
    private void Publish(nint window)
    {
        try
        {
            _publish(window == 0 ? null : ForegroundWindow.GetProcessName(window));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reading the program in front failed.");
        }
    }

    /// <summary>
    /// Stops the system's callback from reaching this hook, unless another hook has taken its place meanwhile.
    /// </summary>
    private void ReleaseActive()
    {
        _ = Interlocked.CompareExchange(ref s_active, null, this);
    }

    /// <summary>
    /// Body of the hook thread: installs the hook, publishes the program that is in front now, and pumps messages, which
    /// is what delivers the events, until asked to end.
    /// </summary>
    private void Run()
    {
        nint hook = 0;
        try
        {
            // Reading the message queue once creates it, which the quit message needs to be posted to this thread.
            _ = PeekMessage(out _, 0, 0, 0, 0);
            bool stopRequested;
            lock (_gate)
            {
                _threadId = GetCurrentThreadId();
                stopRequested = _stopRequested;
            }

            if (stopRequested)
            {
                return;
            }

            s_active = this;
            hook = SetWinEventHook(
                EventSystemForeground,
                EventSystemForeground,
                0,
                &OnWinEvent,
                0,
                0,
                WinEventOutOfContext);
            if (hook == 0)
            {
                _logger.LogWarning("The foreground window hook could not be installed; the foreground program is polled instead.");
                return;
            }

            // The constructor, which may be on the UI thread, is let go before the first lookup: opening the foreground
            // process can wait for another program, and until the first publish arrives the question goes to the fallback.
            IsInstalled = true;
            _ready.Set();
            Publish(ForegroundWindow.GetHandle());

            while (GetMessage(out _, 0, 0, 0) > 0)
            {
            }
        }
        finally
        {
            if (hook != 0)
            {
                _ = UnhookWinEvent(hook);
            }

            ReleaseActive();
            IsInstalled = false;
            _ready.Set();
        }
    }

    /// <summary>
    /// A message of the thread's queue; only filled in by the system, never read.
    /// </summary>
    private struct Message
    {
        /// <summary>
        /// The window the message is for.
        /// </summary>
        public nint Window;

        /// <summary>
        /// The message number.
        /// </summary>
        public uint Id;

        /// <summary>
        /// The first parameter.
        /// </summary>
        public nuint WParam;

        /// <summary>
        /// The second parameter.
        /// </summary>
        public nint LParam;

        /// <summary>
        /// When the message was posted.
        /// </summary>
        public uint Time;

        /// <summary>
        /// The pointer's horizontal position when the message was posted.
        /// </summary>
        public int X;

        /// <summary>
        /// The pointer's vertical position when the message was posted.
        /// </summary>
        public int Y;
    }

    /// <summary>
    /// Installs a hook for events of the system.
    /// </summary>
    /// <param name="eventMin">The first event to receive.</param>
    /// <param name="eventMax">The last event to receive.</param>
    /// <param name="module">Zero, as the callback is in this program.</param>
    /// <param name="callback">Called for each event.</param>
    /// <param name="processId">Zero for the events of every process.</param>
    /// <param name="threadId">Zero for the events of every thread.</param>
    /// <param name="flags">How the callback is called.</param>
    /// <returns>The hook handle, or zero on failure.</returns>
    [LibraryImport("user32.dll")]
    private static partial nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint module,
        delegate* unmanaged[Stdcall]<nint, uint, nint, int, int, uint, uint, void> callback,
        uint processId,
        uint threadId,
        uint flags);

    /// <summary>
    /// Removes a hook of events.
    /// </summary>
    /// <param name="hook">The hook handle.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hook);

    /// <summary>
    /// Waits for the next message of the calling thread; delivers the events of an out-of-context hook while it waits.
    /// </summary>
    /// <param name="message">Receives the message.</param>
    /// <param name="window">Zero for the messages of every window.</param>
    /// <param name="filterMin">Zero for no lower limit.</param>
    /// <param name="filterMax">Zero for no upper limit.</param>
    /// <returns>Positive for a message, zero for the quit message, negative on failure.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    private static partial int GetMessage(out Message message, nint window, uint filterMin, uint filterMax);

    /// <summary>
    /// Looks at the message queue of the calling thread without waiting, and creates the queue if there is none.
    /// </summary>
    /// <param name="message">Receives the message.</param>
    /// <param name="window">Zero for the messages of every window.</param>
    /// <param name="filterMin">Zero for no lower limit.</param>
    /// <param name="filterMax">Zero for no upper limit.</param>
    /// <param name="remove">Zero to leave the message in the queue.</param>
    /// <returns><see langword="true"/> if there was a message.</returns>
    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out Message message, nint window, uint filterMin, uint filterMax, uint remove);

    /// <summary>
    /// Posts a message to the queue of a thread.
    /// </summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="message">The message number.</param>
    /// <param name="wParam">The first parameter.</param>
    /// <param name="lParam">The second parameter.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    /// <summary>
    /// Gets the identifier of the calling thread.
    /// </summary>
    /// <returns>The thread identifier.</returns>
    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();
}
