namespace DeskBuddy.Application.Sensors;

/// <summary>
/// Answers <see cref="IForegroundAppDetector"/> from what the system tells: whenever another window comes to the front
/// something calls <see cref="Push"/> with its program, so the answer is always the current one and nothing is polled.
/// Until the first push arrives, and for good if none ever does (the system refused to report window changes), the
/// question goes to a detector that polls.
/// </summary>
/// <param name="fallback">Answers while nothing has been pushed; it may poll, but must not wait for the system.</param>
public sealed class PushedForegroundApp(IForegroundAppDetector fallback) : IForegroundAppDetector
{
    /// <summary>
    /// Guards the fields below, which the thread that pushes and the thread that asks both touch.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// The program that was last pushed.
    /// </summary>
    private string? _name;

    /// <summary>
    /// Whether anything was pushed yet.
    /// </summary>
    private bool _isPushed;

    /// <inheritdoc />
    public string? GetForegroundProcessName()
    {
        lock (_gate)
        {
            if (_isPushed)
            {
                return _name;
            }
        }

        return fallback.GetForegroundProcessName();
    }

    /// <summary>
    /// Raised, from the thread that pushed, after every push: another window came to the front, which may be of the same
    /// program.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Tells which program is now in front. May be called from any thread.
    /// </summary>
    /// <param name="name">The name of the program without <c>.exe</c>, or <see langword="null"/> if there is none or it cannot be read.</param>
    public void Push(string? name)
    {
        lock (_gate)
        {
            _name = name;
            _isPushed = true;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
