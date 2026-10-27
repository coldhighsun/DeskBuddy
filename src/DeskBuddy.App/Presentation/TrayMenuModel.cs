using System.Windows.Media;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// One pet in the list of pets of the tray menu.
/// </summary>
/// <param name="FolderName">
/// The name of the pet's folder, which is what the settings store.
/// </param>
/// <param name="DisplayName">
/// The name the pet gives itself in its manifest.
/// </param>
/// <param name="Picture">
/// The picture shown for the pet, if it has one.
/// </param>
/// <param name="IsCurrent">
/// Whether this is the pet in use.
/// </param>
internal sealed record PetChoice(string FolderName, string DisplayName, ImageSource? Picture, bool IsCurrent);

/// <summary>
/// What the tray menu shows, worked out from the pets and the settings; kept apart from the menu so it can be tested.
/// </summary>
internal static class TrayMenuModel
{
    /// <summary>
    /// Lists the pets that can be chosen and marks the one in use.
    /// </summary>
    /// <param name="entries">
    /// The pets that can be chosen.
    /// </param>
    /// <param name="currentPet">
    /// The folder name of the pet in use.
    /// </param>
    /// <returns>
    /// One choice per pet, in the order of <paramref name="entries"/>.
    /// </returns>
    public static IReadOnlyList<PetChoice> PetChoices(IReadOnlyList<InstalledPet> entries, string currentPet)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return
        [
            .. entries.Select(entry => new PetChoice(
                entry.FolderName,
                entry.DisplayName,
                entry.Picture,
                string.Equals(entry.FolderName, currentPet, StringComparison.Ordinal))),
        ];
    }
}

/// <summary>
/// Everything the tray menu shows that can change while the application runs.
/// </summary>
/// <param name="PetName">
/// The folder name of the pet in use.
/// </param>
/// <param name="Size">
/// The size in use.
/// </param>
/// <param name="PetHidden">
/// Whether the user hid the pet.
/// </param>
/// <param name="PetsVersion">
/// The version of the list of pets that can be chosen.
/// </param>
/// <param name="UpdateVersion">
/// The newer version of the application waiting to be installed, or <see langword="null"/> if there is none.
/// </param>
internal sealed record TrayMenuState(string PetName, PetSize Size, bool PetHidden, int PetsVersion, string? UpdateVersion = null);

/// <summary>
/// Decides when the tray menu has to be built again: only when what it shows has changed, and never while it is open,
/// as that would pull the items from under the user's cursor.
/// </summary>
internal sealed class TrayMenuRebuildPolicy
{
    /// <summary>
    /// The state the menu was last built for; <see langword="null"/> before the first build.
    /// </summary>
    private TrayMenuState? _built;

    /// <summary>
    /// Asks whether the menu has to be built again. Ask again when the menu closes: a change that came while it was
    /// open is still waiting.
    /// </summary>
    /// <param name="state">
    /// What the menu has to show now.
    /// </param>
    /// <param name="menuOpen">
    /// Whether the menu is open.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the menu has to be built now.
    /// </returns>
    public bool ShouldRebuild(TrayMenuState state, bool menuOpen)
    {
        return !menuOpen && state != _built;
    }

    /// <summary>
    /// Records that the menu was built for a state.
    /// </summary>
    /// <param name="state">
    /// What the menu was built to show.
    /// </param>
    public void MarkBuilt(TrayMenuState state)
    {
        _built = state;
    }
}
