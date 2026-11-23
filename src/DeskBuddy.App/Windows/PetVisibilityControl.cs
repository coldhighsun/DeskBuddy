using System.Windows.Threading;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Lets the tray icon, the global shortcut and the start-up code hide and show the pet although the pet, its brain and
/// its window live on a thread of their own: a request is handed over to that thread, and the answer to "is it hidden" is
/// read from a field the pet's thread keeps up to date. The change is reported on the thread of the caller that made this.
/// </summary>
public sealed class PetVisibilityControl : IPetVisibilityControl, IDisposable
{
    /// <summary>
    /// Hands work over to the pet's thread.
    /// </summary>
    private readonly Action<Action> _post;

    /// <summary>
    /// The brain, which belongs to the pet's thread: it is touched only through <see cref="_post"/>.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// The dispatcher of the caller's thread, on which the change is reported.
    /// </summary>
    private readonly Dispatcher _caller;

    /// <summary>
    /// Whether the control has been disposed, after which a change that is on its way is not reported.
    /// </summary>
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetVisibilityControl"/> class.
    /// </summary>
    /// <param name="post">Hands work over to the pet's thread, without waiting for it.</param>
    /// <param name="brain">The brain of the pet.</param>
    /// <param name="caller">The dispatcher of the thread the change is reported on.</param>
    public PetVisibilityControl(Action<Action> post, PetBrain brain, Dispatcher caller)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(caller);

        _post = post;
        _brain = brain;
        _caller = caller;
        _brain.VisibilityChanged += OnBrainVisibilityChanged;
    }

    /// <inheritdoc />
    public event EventHandler? VisibilityChanged;

    /// <inheritdoc />
    public bool IsManuallyHidden => _brain.Visibility.IsManuallyHidden;

    /// <inheritdoc />
    public void ToggleManuallyHidden()
    {
        _post(_brain.ToggleManuallyHidden);
    }

    /// <inheritdoc />
    public void SetManuallyHidden(bool hidden)
    {
        _post(() => _brain.SetManuallyHidden(hidden));
    }

    /// <inheritdoc />
    public void SetSessionLocked(bool locked)
    {
        _post(() => _brain.SetSessionLocked(locked));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _brain.VisibilityChanged -= OnBrainVisibilityChanged;
    }

    /// <summary>
    /// Passes a change the brain reports, on the pet's thread, on to the caller's thread.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event data.</param>
    private void OnBrainVisibilityChanged(object? sender, EventArgs e)
    {
        _caller.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                VisibilityChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }
}
