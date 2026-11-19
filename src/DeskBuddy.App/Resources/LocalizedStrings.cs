using System.ComponentModel;
using System.Reflection;

namespace DeskBuddy.App.Resources;

/// <summary>
/// The text of <see cref="Strings"/> by key, as something XAML can bind to: when the interface language changes,
/// <see cref="NotifyLanguageChanged"/> makes every binding read its text again, so open windows change language without
/// being built anew. See <see cref="Loc"/> for the markup extension that uses it.
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the one instance every binding reads from.
    /// </summary>
    public static LocalizedStrings Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedStrings"/> class; use <see cref="Instance"/>.
    /// </summary>
    private LocalizedStrings()
    {
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Who listens to <see cref="LanguageChanged"/>: the object, held weakly, and the method to call on it.
    /// </summary>
    private readonly List<(WeakReference<object>? Target, MethodInfo Method)> _listeners = [];

    /// <summary>
    /// Occurs after the interface language has changed, for view models that hold text of their own and write it again.
    /// The listeners are held weakly: this instance lives as long as the process, and a listener that forgot to remove
    /// itself would otherwise be kept alive, and called, for just as long.
    /// </summary>
    public event EventHandler? LanguageChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_listeners)
            {
                foreach (var handler in value.GetInvocationList())
                {
                    _listeners.Add((handler.Target is { } target ? new WeakReference<object>(target) : null, handler.Method));
                }
            }
        }

        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_listeners)
            {
                foreach (var handler in value.GetInvocationList())
                {
                    var index = _listeners.FindIndex(listener => listener.Method == handler.Method && IsTarget(listener.Target, handler.Target));
                    if (index >= 0)
                    {
                        _listeners.RemoveAt(index);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the text for a key in the interface language now in use.
    /// </summary>
    /// <param name="key">
    /// The name of the text in <c>Strings.resx</c>.
    /// </param>
    /// <returns>
    /// The text, or <c>!key!</c> if there is none.
    /// </returns>
    public string this[string key] => Strings.Lookup(key);

    /// <summary>
    /// Tells everything that shows text that the interface language has changed. Call on the UI thread, after the culture
    /// has been switched.
    /// </summary>
    public void NotifyLanguageChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

        List<(object? Target, MethodInfo Method)> alive = [];
        lock (_listeners)
        {
            _listeners.RemoveAll(listener => listener.Target is { } weak && !weak.TryGetTarget(out _));
            foreach (var (weak, method) in _listeners)
            {
                if (weak is null)
                {
                    alive.Add((null, method));
                }
                else if (weak.TryGetTarget(out var target))
                {
                    alive.Add((target, method));
                }
            }
        }

        foreach (var (target, method) in alive)
        {
            method.Invoke(target, [this, EventArgs.Empty]);
        }
    }

    /// <summary>
    /// Tells whether a weakly held listener is a given object.
    /// </summary>
    /// <param name="held">
    /// The weak reference of the listener, or <see langword="null"/> for a static method.
    /// </param>
    /// <param name="target">
    /// The object to compare with, or <see langword="null"/> for a static method.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if both are the same object (or both are static).
    /// </returns>
    private static bool IsTarget(WeakReference<object>? held, object? target)
    {
        return held is null
            ? target is null
            : target is not null && held.TryGetTarget(out var alive) && ReferenceEquals(alive, target);
    }
}
