using DeskBuddy.Domain.Power;
using Microsoft.Win32;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Passes on the power status each time Windows reports that the power supply changed (the cable plugged in or pulled
/// out, the charge changing, the computer waking up), instead of reading it at every tick. The system raises these events
/// on a thread of its own.
/// </summary>
public sealed class SystemEventsPowerWatcher : IDisposable
{
    /// <summary>
    /// Reads the power status when an event says it changed.
    /// </summary>
    private readonly IPowerStatusProvider _reader;

    /// <summary>
    /// Where the new status is passed on to.
    /// </summary>
    private readonly Action<PowerStatus> _publish;

    /// <summary>
    /// Where a failed reading is logged.
    /// </summary>
    private readonly ILogger<SystemEventsPowerWatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemEventsPowerWatcher"/> class, publishes the status as it is now,
    /// and starts listening.
    /// </summary>
    /// <param name="reader">Reads the power status.</param>
    /// <param name="publish">Called with the status at the start and after each change, from any thread.</param>
    /// <param name="logger">Where a failed reading is logged.</param>
    public SystemEventsPowerWatcher(
        IPowerStatusProvider reader,
        Action<PowerStatus> publish,
        ILogger<SystemEventsPowerWatcher> logger)
    {
        _reader = reader;
        _publish = publish;
        _logger = logger;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Publish();
    }

    /// <summary>
    /// Stops listening.
    /// </summary>
    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    /// <summary>
    /// Decides whether a power event can have changed the status the pet cares about.
    /// </summary>
    /// <param name="mode">The kind of power event.</param>
    /// <returns>
    /// <see langword="true"/> for a change of the power status (the cable, the charge) and for waking up, when
    /// anything may have changed meanwhile; <see langword="false"/> for going to sleep.
    /// </returns>
    public static bool ChangesStatus(PowerModes mode)
    {
        return mode is PowerModes.StatusChange or PowerModes.Resume;
    }

    /// <summary>
    /// Reacts to a power event.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Which event happened.</param>
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (ChangesStatus(e.Mode))
        {
            Publish();
        }
    }

    /// <summary>
    /// Reads the power status and passes it on.
    /// </summary>
    private void Publish()
    {
        try
        {
            _publish(_reader.GetStatus());
        }
        catch (Exception ex)
        {
            // This runs on a thread of the system's; an exception there would end the process.
            _logger.LogWarning(ex, "Reading the power status after a power event failed.");
        }
    }
}
