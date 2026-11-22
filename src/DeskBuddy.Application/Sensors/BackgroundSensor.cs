using System.Diagnostics;

namespace DeskBuddy.Application.Sensors;

/// <summary>
/// Reads a sensor on a thread of the thread pool and hands out the last reading at once. The pet's brain ticks on the UI
/// thread, and a system call that waits for another program (the shell, a process that is busy, a disk under a build)
/// would freeze the whole pet for as long as it waits; with this, only the reading waits and the pet carries on with the
/// last answer. The answer is therefore one reading old: it comes from the previous call that asked for a new one. When
/// something tells that the answer is probably out of date, <see cref="Refresh"/> reads at once, and
/// <see cref="Changed"/> tells when a reading came out different.
/// </summary>
/// <typeparam name="T">The kind of reading.</typeparam>
/// <param name="name">What the sensor is called in the log.</param>
/// <param name="read">Does the real reading; runs on a thread of the thread pool, never on two at once.</param>
/// <param name="initial">The answer until the first reading is done.</param>
/// <param name="interval">The shortest time between the starts of two readings asked for by <see cref="GetValue"/>.</param>
/// <param name="timeProvider">The time source.</param>
/// <param name="diagnostics">Told how long readings take and which fail, or <see langword="null"/> to say nothing.</param>
/// <param name="run">Runs a reading; the thread pool unless a test wants it on the spot.</param>
public sealed class BackgroundSensor<T>(
    string name,
    Func<T> read,
    T initial,
    TimeSpan interval,
    TimeProvider timeProvider,
    SensorDiagnostics? diagnostics = null,
    Action<Action>? run = null)
{
    /// <summary>
    /// Guards the fields below, which the reading thread and the caller both touch.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// The last reading.
    /// </summary>
    private T _value = initial;

    /// <summary>
    /// When the last reading was started, or <see langword="null"/> if none was.
    /// </summary>
    private DateTimeOffset? _startedAt;

    /// <summary>
    /// Whether a reading is under way; a slow one is waited for, never started a second time next to it.
    /// </summary>
    private bool _reading;

    /// <summary>
    /// Whether a refresh was asked for while a reading was under way, which then reads once more when it is done: what it
    /// started with may already be out of date.
    /// </summary>
    private bool _readAgain;

    /// <summary>
    /// Raised, from the thread that did the reading, when a reading comes out different from the one before. Not raised
    /// for the first reading when it equals the initial answer.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the last reading and, if the interval has passed and no reading is under way, starts the next one.
    /// </summary>
    /// <returns>The last reading, or the initial answer if there is none yet. Never waits for the system.</returns>
    public T GetValue()
    {
        var start = false;
        T value;
        lock (_gate)
        {
            value = _value;
            var now = timeProvider.GetUtcNow();
            if (!_reading && (_startedAt is not { } startedAt || now - startedAt >= interval))
            {
                _reading = true;
                _startedAt = now;
                start = true;
            }
        }

        if (start)
        {
            (run ?? RunOnThreadPool)(Read);
        }

        return value;
    }

    /// <summary>
    /// Reads now, whatever the interval, because something says the last reading is probably out of date. A reading that
    /// is already under way is followed by one more. Never waits for the system.
    /// </summary>
    public void Refresh()
    {
        var start = false;
        lock (_gate)
        {
            if (_reading)
            {
                _readAgain = true;
            }
            else
            {
                _reading = true;
                _startedAt = timeProvider.GetUtcNow();
                start = true;
            }
        }

        if (start)
        {
            (run ?? RunOnThreadPool)(Read);
        }
    }

    /// <summary>
    /// Runs an action on a thread of the thread pool.
    /// </summary>
    /// <param name="action">What to run.</param>
    private static void RunOnThreadPool(Action action)
    {
        _ = Task.Run(action);
    }

    /// <summary>
    /// Raises <see cref="Changed"/>; a listener that throws is reported and does not stop the readings.
    /// </summary>
    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            diagnostics?.Failed(name, ex);
        }
    }

    /// <summary>
    /// Reads, keeps the result and reports how long it took, as many times as were asked for while it ran.
    /// </summary>
    private void Read()
    {
        bool again;
        do
        {
            var startedAt = Stopwatch.GetTimestamp();
            var changed = false;
            try
            {
                var value = read();
                lock (_gate)
                {
                    changed = !EqualityComparer<T>.Default.Equals(_value, value);
                    _value = value;
                }
            }
            catch (Exception ex)
            {
                // The last reading stays; a sensor that fails must not take the pet down from a pool thread.
                diagnostics?.Failed(name, ex);
            }
            finally
            {
                lock (_gate)
                {
                    again = _readAgain;
                    _readAgain = false;
                    _reading = again;
                }

                diagnostics?.Timed(name, Stopwatch.GetElapsedTime(startedAt));
            }

            if (changed)
            {
                RaiseChanged();
            }
        }
        while (again);
    }
}
