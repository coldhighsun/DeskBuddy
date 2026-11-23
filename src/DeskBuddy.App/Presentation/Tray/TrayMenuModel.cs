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
    /// <param name="maxPets">
    /// The most pets to list, <see cref="MaxPetsShown"/> by default. When more pets are offered, the first ones in the order
    /// of <paramref name="entries"/> are kept, and the pet in use takes the place of the last one if it would be cut, so the
    /// menu always shows what is on the screen. The rest are reached from the panel's list of pets.
    /// </param>
    /// <returns>One choice per pet, in the order of <paramref name="entries"/>, at most <paramref name="maxPets"/>.</returns>
    public static IReadOnlyList<PetChoice> PetChoices(
        IReadOnlyList<InstalledPet> entries,
        string currentPet,
        IReadOnlyList<string>? hidden = null,
        int maxPets = MaxPetsShown)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var offered = entries
            .Where(entry => IsOffered(entry.FolderName, currentPet, hidden))
            .ToList();
        var limit = Math.Max(maxPets, 1);
        if (offered.Count > limit)
        {
            var kept = offered.Take(limit).ToList();
            var current = offered.Find(entry => string.Equals(entry.FolderName, currentPet, StringComparison.Ordinal));
            if (current is not null && !kept.Contains(current))
            {
                kept[^1] = current;
            }

            offered = kept;
        }

        return
        [
            .. offered.Select(entry => new PetChoice(
                entry.FolderName,
                entry.DisplayName,
                entry.Picture,
                string.Equals(entry.FolderName, currentPet, StringComparison.Ordinal))),
        ];
    }

    /// <summary>
    /// The most pets listed in the grid of the submenu that switches the pet; more pets than that are chosen from the
    /// panel's list of pets, which the "All pets" entry opens. Random pet still picks from every pet that is offered.
    /// </summary>
    public const int MaxPetsShown = 36;

    /// <summary>
    /// Gets how many pets sit side by side in the grid of the submenu that switches the pet. The grid is sized to the number
    /// of pets and kept close to a square (the square root of the count, rounded up), but never narrower than
    /// <see cref="MinColumns"/>, so the full-width entries below the grid keep their text, and never wider than
    /// <see cref="MaxColumns"/>.
    /// </summary>
    /// <param name="petCount">How many pets there are to show.</param>
    /// <returns>The number of columns.</returns>
    public static int GridColumns(int petCount)
    {
        var square = (int)Math.Ceiling(Math.Sqrt(Math.Max(petCount, 0)));
        return Math.Clamp(square, MinColumns, MaxColumns);
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
    /// The fewest columns of the pet grid: wide enough for the text of the full-width entries below it.
    /// </summary>
    private const int MinColumns = 3;

    /// <summary>
    /// The most columns of the pet grid; more pets than that make the grid taller, and it scrolls.
    /// </summary>
    private const int MaxColumns = 8;
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
