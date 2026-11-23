using System.Windows.Media;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// One pet in the list of pets of the tray menu.
/// </summary>
/// <param name="FolderName">The name of the pet's folder, which is what the settings store.</param>
/// <param name="DisplayName">The name the pet gives itself in its manifest.</param>
/// <param name="Picture">The picture shown for the pet, if it has one.</param>
/// <param name="IsCurrent">Whether this is the pet in use.</param>
internal sealed record PetChoice(string FolderName, string DisplayName, ImageSource? Picture, bool IsCurrent);

/// <summary>
/// What the tray menu shows, worked out from the pets and the settings; kept apart from the menu so it can be tested.
/// </summary>
internal static class TrayMenuModel
{
    /// <summary>
    /// Lists the pets that can be chosen and marks the one in use.
    /// </summary>
    /// <param name="entries">The pets that can be chosen.</param>
    /// <param name="currentPet">The folder name of the pet in use.</param>
    /// <param name="hidden">
    /// The folder names of the pets the user has switched off, which are left out, or <see langword="null"/> for none. The
    /// pet in use is never left out.
    /// </param>
    /// <returns>One choice per pet, in the order of <paramref name="entries"/>.</returns>
    public static IReadOnlyList<PetChoice> PetChoices(IReadOnlyList<InstalledPet> entries, string currentPet, IReadOnlyList<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return
        [
            .. entries
                .Where(entry => IsOffered(entry.FolderName, currentPet, hidden))
                .Select(entry => new PetChoice(
                entry.FolderName,
                entry.DisplayName,
                entry.Picture,
                string.Equals(entry.FolderName, currentPet, StringComparison.Ordinal))),
        ];
    }

    /// <summary>
    /// Gets how many pets sit side by side in the grid of the submenu that switches the pet, so the grid grows wider before
    /// it grows too tall: four columns up to sixteen pets, five up to twenty-five, six beyond that.
    /// </summary>
    /// <param name="petCount">How many pets there are to show.</param>
    /// <returns>The number of columns.</returns>
    public static int GridColumns(int petCount)
    {
        return petCount switch
        {
            <= MaxPetsInFourColumns => 4,
            <= MaxPetsInFiveColumns => 5,
            _ => 6,
        };
    }

    /// <summary>
    /// Tells whether there is a pet other than the one in use to change to, compared the way <see cref="PickRandom"/> does.
    /// </summary>
    /// <param name="entries">The pets that can be chosen.</param>
    /// <param name="currentPet">The folder name of the pet in use.</param>
    /// <param name="hidden">The folder names of the pets the user has switched off, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> if at least one pet that is not switched off is not the pet in use.</returns>
    public static bool HasAnotherPet(IReadOnlyList<InstalledPet> entries, string currentPet, IReadOnlyList<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Any(entry => !string.Equals(entry.FolderName, currentPet, StringComparison.OrdinalIgnoreCase)
            && IsOffered(entry.FolderName, currentPet, hidden));
    }

    /// <summary>
    /// Chooses a pet at random to change to, other than the one in use.
    /// </summary>
    /// <param name="entries">The pets that can be chosen.</param>
    /// <param name="currentPet">The folder name of the pet in use.</param>
    /// <param name="random">The random source.</param>
    /// <param name="hidden">The folder names of the pets the user has switched off, or <see langword="null"/> for none.</param>
    /// <returns>The folder name of the pet to change to, or <see langword="null"/> if there is no other pet.</returns>
    public static string? PickRandom(IReadOnlyList<InstalledPet> entries, string currentPet, Random random, IReadOnlyList<string>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(random);

        return AutoSwitchPolicy.PickNext(
            [.. entries.Select(entry => entry.FolderName).Where(name => IsOffered(name, currentPet, hidden))],
            currentPet,
            random);
    }

    /// <summary>
    /// Tells whether a pet is one the user can change to: it is, unless the user has switched it off; the pet in use always
    /// is, so that the menu still shows what is on the screen.
    /// </summary>
    /// <param name="folderName">The folder name of the pet.</param>
    /// <param name="currentPet">The folder name of the pet in use.</param>
    /// <param name="hidden">The folder names of the pets the user has switched off, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> if the pet is offered.</returns>
    internal static bool IsOffered(string folderName, string currentPet, IReadOnlyList<string>? hidden)
    {
        return string.Equals(folderName, currentPet, StringComparison.Ordinal)
            || hidden is null
            || !hidden.Contains(folderName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The most pets shown in a grid of four columns.
    /// </summary>
    private const int MaxPetsInFourColumns = 16;

    /// <summary>
    /// The most pets shown in a grid of five columns.
    /// </summary>
    private const int MaxPetsInFiveColumns = 25;
}

/// <summary>
/// Everything the tray menu shows that can change while the application runs.
/// </summary>
/// <param name="PetName">The folder name of the pet in use.</param>
/// <param name="Size">The size in use.</param>
/// <param name="PetHidden">Whether the user hid the pet.</param>
/// <param name="PetsVersion">The version of the list of pets that can be chosen.</param>
/// <param name="HiddenPets">
/// The folder names of the pets the user has switched off, joined into one text, so that switching one on or off builds the
/// menu again.
/// </param>
/// <param name="UpdateVersion">
/// The newer version of the application waiting to be installed, or <see langword="null"/> if there is none.
/// </param>
/// <param name="Language">
/// The name of the interface language the menu is written in, so a change of language builds the menu again.
/// </param>
internal sealed record TrayMenuState(
    string PetName,
    PetSize Size,
    bool PetHidden,
    int PetsVersion,
    string? UpdateVersion = null,
    string Language = "",
    string HiddenPets = "");

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
    /// <param name="state">What the menu has to show now.</param>
    /// <param name="menuOpen">Whether the menu is open.</param>
    /// <returns><see langword="true"/> if the menu has to be built now.</returns>
    public bool ShouldRebuild(TrayMenuState state, bool menuOpen)
    {
        return !menuOpen && state != _built;
    }

    /// <summary>
    /// Records that the menu was built for a state.
    /// </summary>
    /// <param name="state">What the menu was built to show.</param>
    public void MarkBuilt(TrayMenuState state)
    {
        _built = state;
    }
}
