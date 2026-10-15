namespace DeskBuddy.Application.Settings;

/// <summary>
/// Holds the settings a user is editing until they are saved. The draft is the saved settings with the user's edits laid
/// over them, so when the saved settings change elsewhere (the tray menu switches the size, say) the fields the user did
/// not touch follow along while the edited ones keep the user's value. Nothing reaches the <see cref="SettingsService"/>
/// before <see cref="SaveAsync"/>.
/// </summary>
public sealed class SettingsEditor : IDisposable
{
    /// <summary>
    /// The saved settings the draft is built on.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// The edits in the order they were first made; editing a field again replaces its change in place.
    /// </summary>
    private readonly List<Edit> _edits = [];

    /// <summary>
    /// The explanation of each field that cannot be saved as it stands, by field key.
    /// </summary>
    private readonly Dictionary<string, string> _errors = [];

    /// <summary>
    /// Creates an editor with nothing edited yet.
    /// </summary>
    /// <param name="settings">
    /// The saved settings to edit.
    /// </param>
    public SettingsEditor(SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        Draft = settings.Current;
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    /// Occurs when the draft, the unsaved state, an error or the save outcome changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the settings as they would be after saving.
    /// </summary>
    public PetSettings Draft { get; private set; }

    /// <summary>
    /// Gets the settings that are in effect now.
    /// </summary>
    public PetSettings Baseline => _settings.Current;

    /// <summary>
    /// Gets a value indicating whether the draft differs from the settings in effect.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Gets a value indicating whether some field holds a value that cannot be saved.
    /// </summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>
    /// Gets a value indicating whether saving would do something: there is a change, nothing in it is invalid, and the
    /// saved settings could be read at startup (otherwise saving would overwrite them).
    /// </summary>
    public bool CanSave => IsDirty && !HasErrors && _settings.LoadFailure is null;

    /// <summary>
    /// Gets the reason the last save to disk failed, or <see langword="null"/> if it did not fail.
    /// </summary>
    public Exception? SaveFailure { get; private set; }

    /// <summary>
    /// Sets how one field differs from the saved settings. A later call with the same key replaces the earlier change,
    /// so typing into a box does not pile up edits.
    /// </summary>
    /// <param name="key">
    /// Names the field.
    /// </param>
    /// <param name="change">
    /// Produces the settings with the field changed.
    /// </param>
    public void Set(string key, Func<PetSettings, PetSettings> change)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(change);

        var index = _edits.FindIndex(edit => edit.Key == key);
        if (index >= 0)
        {
            _edits[index] = new Edit(key, change);
        }
        else
        {
            _edits.Add(new Edit(key, change));
        }

        Rebuild();
    }

    /// <summary>
    /// Takes back the edit of one field, so it follows the saved settings again.
    /// </summary>
    /// <param name="key">
    /// Names the field.
    /// </param>
    public void Clear(string key)
    {
        if (_edits.RemoveAll(edit => edit.Key == key) > 0)
        {
            Rebuild();
        }
    }

    /// <summary>
    /// Records that a field holds a value that cannot be saved, or, with <see langword="null"/>, that it is fine again.
    /// </summary>
    /// <param name="key">
    /// Names the field.
    /// </param>
    /// <param name="message">
    /// What is wrong, or <see langword="null"/> or empty when nothing is.
    /// </param>
    public void SetError(string key, string? message)
    {
        var changed = string.IsNullOrEmpty(message)
            ? _errors.Remove(key)
            : !_errors.TryGetValue(key, out var existing) || existing != message;
        if (!string.IsNullOrEmpty(message))
        {
            _errors[key] = message;
        }

        if (changed)
        {
            RaiseChanged();
        }
    }

    /// <summary>
    /// Gets what is wrong with a field.
    /// </summary>
    /// <param name="key">
    /// Names the field.
    /// </param>
    /// <returns>
    /// The explanation, or an empty string if the field is fine.
    /// </returns>
    public string ErrorFor(string key)
    {
        return _errors.GetValueOrDefault(key, string.Empty);
    }

    /// <summary>
    /// Throws away every edit and error, so the draft is the saved settings again.
    /// </summary>
    public void Revert()
    {
        _edits.Clear();
        _errors.Clear();
        SaveFailure = null;
        Rebuild();
    }

    /// <summary>
    /// Saves the draft. The edits are laid over whatever is saved at that moment, so a change made elsewhere in the
    /// meantime is kept. Only the edits that existed when saving began are saved and dropped afterwards; an edit made
    /// while the save is running stays in the draft. When writing to disk fails the settings are still in effect in
    /// memory, so those edits are dropped and the failure is kept in <see cref="SaveFailure"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the settings were saved; <see langword="false"/> if there was nothing to save, a field
    /// is invalid, or writing failed.
    /// </returns>
    public async Task<bool> SaveAsync()
    {
        if (!CanSave)
        {
            return false;
        }

        var snapshot = _edits.ToArray();
        var saved = false;
        try
        {
            await _settings.UpdateAsync(settings => Apply(settings, snapshot));
            SaveFailure = null;
            saved = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveFailure = ex;
        }
        catch (InvalidOperationException)
        {
            // Raised only after a failed load, which CanSave rules out; nothing was changed.
            return false;
        }

        _edits.RemoveAll(edit => snapshot.Any(done => ReferenceEquals(done, edit)));
        Rebuild();
        return saved;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>
    /// Lays edits over some settings.
    /// </summary>
    /// <param name="settings">
    /// The settings to start from.
    /// </param>
    /// <param name="edits">
    /// The edits to apply, in order.
    /// </param>
    /// <returns>
    /// The settings with every edit applied.
    /// </returns>
    private static PetSettings Apply(PetSettings settings, IEnumerable<Edit> edits)
    {
        return edits.Aggregate(settings, (current, edit) => edit.Change(current));
    }

    /// <summary>
    /// Builds the draft again after the edits or the saved settings changed.
    /// </summary>
    private void Rebuild()
    {
        Draft = Apply(Baseline, _edits);
        IsDirty = !PetSettingsComparer.Instance.Equals(Draft, Baseline);
        RaiseChanged();
    }

    /// <summary>
    /// Follows the saved settings when they change, keeping the edits.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new saved settings.
    /// </param>
    private void OnSettingsChanged(object? sender, PetSettings settings)
    {
        Rebuild();
    }

    /// <summary>
    /// Raises <see cref="Changed"/>.
    /// </summary>
    private void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// One edited field.
    /// </summary>
    /// <param name="Key">
    /// Names the field.
    /// </param>
    /// <param name="Change">
    /// Produces the settings with the field changed.
    /// </param>
    private sealed record Edit(string Key, Func<PetSettings, PetSettings> Change);
}
