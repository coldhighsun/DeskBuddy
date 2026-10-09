using DeskBuddy.Domain.Skins;

namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Loads skin packs from storage.
/// </summary>
public interface ISkinLoader
{
    /// <summary>
    /// Loads the skin located in the given directory.
    /// </summary>
    /// <param name="skinDirectory">
    /// Directory containing the skin manifest and frame images.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The loaded skin.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the manifest is missing, malformed, or references missing frames.
    /// </exception>
    Task<Skin> LoadAsync(string skinDirectory, CancellationToken cancellationToken = default);
}
