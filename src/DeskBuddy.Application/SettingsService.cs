using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Application;

/// <summary>
/// Holds the current settings and saves them after every change. Changes take effect in memory immediately, saves are
/// serialized, and a failed save is reported to the caller rather than swallowed. If the saved settings could not be
/// read at startup, defaults are used and changes are refused so the unreadable file is never overwritten.
/// </summary>
/// <param name="store">
/// Where the settings are saved.
/// </param>
public sealed class SettingsService(ISettingsStore store)
{
    /// <summary>
    /// Makes sure only one change-and-save runs at a time.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Raised after the settings changed in memory (before the save finishes).
    /// </summary>
    public event EventHandler<PetSettings>? Changed;

    /// <summary>
    /// Gets the current settings, always normalized.
    /// </summary>
    public PetSettings Current { get; private set; } = PetSettings.Default;

    /// <summary>
    /// Gets why loading failed at startup, or <see langword="null"/> if it succeeded. While set, the defaults are in
    /// use and every change is refused.
    /// </summary>
    public Exception? LoadFailure { get; private set; }

    /// <summary>
    /// Gets the process names that were taken out of the lists when the settings were loaded, because they broke the
    /// rules for the lists; empty if there were none.
    /// </summary>
    public IReadOnlyList<RemovedProcessName> RemovedAtLoad { get; private set; } = [];

    /// <summary>
    /// Loads the saved settings. A storage problem is recorded in <see cref="LoadFailure"/> instead of thrown, so the
    /// application can still start.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when loading has finished.
    /// </returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var enforced = ProcessListRules.Enforce((await store.LoadAsync(cancellationToken)).Normalize());
            Current = enforced.Settings;
            RemovedAtLoad = enforced.Removed;
            LoadFailure = null;
            Changed?.Invoke(this, Current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LoadFailure = ex;
        }
    }

    /// <summary>
    /// Changes the settings and saves. The result of the change is normalized, so out-of-range values are corrected
    /// rather than rejected.
    /// </summary>
    /// <param name="change">
    /// Produces the new settings from the current ones.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The settings now in effect.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when loading failed at startup, so changes are refused.
    /// </exception>
    public async Task<PetSettings> UpdateAsync(
        Func<PetSettings, PetSettings> change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (LoadFailure is not null)
        {
            throw new InvalidOperationException(
                "The saved settings could not be read, so changes are refused to avoid overwriting them.",
                LoadFailure);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Current = ProcessListRules.Enforce(change(Current).Normalize()).Settings;
            Changed?.Invoke(this, Current);
            await store.SaveAsync(Current, cancellationToken);
            return Current;
        }
        finally
        {
            _gate.Release();
        }
    }
}
