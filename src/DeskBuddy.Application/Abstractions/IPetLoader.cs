using DeskBuddy.Domain.Pets;

namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Loads pet packs from storage.
/// </summary>
public interface IPetLoader
{
    /// <summary>
    /// Loads the pet located in the given directory.
    /// </summary>
    /// <param name="petDirectory">
    /// Directory containing the pet manifest and frame images.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The loaded pet.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the manifest is missing, malformed, or references missing frames.
    /// </exception>
    Task<Pet> LoadAsync(string petDirectory, CancellationToken cancellationToken = default);
}
