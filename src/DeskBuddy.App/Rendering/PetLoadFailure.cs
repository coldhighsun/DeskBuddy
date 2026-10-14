using System.IO;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Tells the failures that come with a pet that cannot be used from other errors: a missing or broken manifest, files that
/// cannot be read or pictures that cannot be decoded. Everything that loads or shows a pet treats these the same way.
/// </summary>
internal static class PetLoadFailure
{
    /// <summary>
    /// Asks whether an exception means that a pet cannot be loaded or shown.
    /// </summary>
    /// <param name="exception">
    /// The exception.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the pet is at fault, so the caller can leave it out or keep the old one.
    /// </returns>
    public static bool Is(Exception exception)
    {
        return exception is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or FormatException;
    }
}
