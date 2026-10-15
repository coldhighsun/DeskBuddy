namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Persists the user's settings.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads the saved settings.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The saved settings, normalized; <see cref="PetSettings.Default"/> if nothing usable has been saved yet.
    /// </returns>
    /// <exception cref="IOException">
    /// Thrown when the storage cannot be read.
    /// </exception>
    Task<PetSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the saved settings.
    /// </summary>
    /// <param name="settings">
    /// The complete settings.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <exception cref="IOException">
    /// Thrown when the storage cannot be written.
    /// </exception>
    Task SaveAsync(PetSettings settings, CancellationToken cancellationToken = default);
}
