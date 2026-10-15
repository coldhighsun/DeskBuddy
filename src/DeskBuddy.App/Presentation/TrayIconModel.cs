using System.Windows.Media;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// The picture the tray icon is made of.
/// </summary>
/// <param name="Picture">
/// The picture, or <see langword="null"/> if the pet in use has none and the icon is the application's own.
/// </param>
/// <param name="IsIconPicture">
/// Whether it is the picture the pet brings for the icon (its head) and is used as it is, as against its idle picture, of
/// which only the upper part is used.
/// </param>
internal sealed record TrayIconPicture(ImageSource? Picture, bool IsIconPicture);

/// <summary>
/// Decides which picture the tray icon shows, from the pets and the pet in use; kept apart from the tray service so it can
/// be tested.
/// </summary>
internal static class TrayIconModel
{
    /// <summary>
    /// Chooses the picture for the tray icon: the icon picture of the pet in use, else its idle picture; if that pet is not
    /// among the pets that could be loaded, the same for the fallback pet (which is the pet that is shown in that case).
    /// </summary>
    /// <param name="entries">
    /// The pets that can be chosen.
    /// </param>
    /// <param name="petName">
    /// The folder name of the pet in use.
    /// </param>
    /// <param name="fallbackPetName">
    /// The folder name of the pet that is shown when the pet in use cannot be loaded.
    /// </param>
    /// <returns>
    /// The picture to make the icon of.
    /// </returns>
    public static TrayIconPicture Choose(IReadOnlyList<InstalledPet> entries, string petName, string fallbackPetName)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entry = entries.FirstOrDefault(pet => string.Equals(pet.FolderName, petName, StringComparison.Ordinal))
            ?? entries.FirstOrDefault(pet => string.Equals(pet.FolderName, fallbackPetName, StringComparison.Ordinal));
        return entry?.IconPicture is { } icon
            ? new TrayIconPicture(icon, IsIconPicture: true)
            : new TrayIconPicture(entry?.Picture, IsIconPicture: false);
    }
}
