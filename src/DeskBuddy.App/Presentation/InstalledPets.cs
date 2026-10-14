using System.IO;
using System.Windows.Media;
using DeskBuddy.App.Rendering;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// One installed pet, as the tray menu and the panel show it.
/// </summary>
/// <param name="FolderName">
/// The name of the pet's folder, which is what the settings store.
/// </param>
/// <param name="DisplayName">
/// The name the pet gives itself in its manifest.
/// </param>
/// <param name="Picture">
/// The picture the menu shows for the pet, or <see langword="null"/> if it has none or it cannot be decoded.
/// </param>
/// <param name="FrameWidth">
/// The width of the pet's pictures, as the pet declares it.
/// </param>
/// <param name="FrameHeight">
/// The height of the pet's pictures, as the pet declares it.
/// </param>
public sealed record InstalledPet(string FolderName, string DisplayName, ImageSource? Picture, int FrameWidth, int FrameHeight);

/// <summary>
/// The installed pets as the tray menu and the panel show them. A pet that cannot be loaded is left out, and tried again the next time
/// the list is refreshed, so a pet that was repaired appears without a restart; a pet that was removed disappears.
/// </summary>
public sealed class InstalledPets
{
    /// <summary>
    /// Lists the installed pets.
    /// </summary>
    private readonly IPetCatalog _catalog;

    /// <summary>
    /// Loads a pet from its folder.
    /// </summary>
    private readonly IPetLoader _loader;

    /// <summary>
    /// The folder the pets are installed in.
    /// </summary>
    private readonly string _petsDirectory;

    /// <summary>
    /// Supplies the picture in a file.
    /// </summary>
    private readonly Func<string, ImageSource?> _frames;

    /// <summary>
    /// Lets one refresh at a time run, as the list is refreshed from the start-up and from every opening of the menu.
    /// </summary>
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="InstalledPets"/> class.
    /// </summary>
    /// <param name="catalog">
    /// Lists the installed pets.
    /// </param>
    /// <param name="loader">
    /// Loads a pet from its folder.
    /// </param>
    /// <param name="petsDirectory">
    /// The folder the pets are installed in.
    /// </param>
    /// <param name="frames">
    /// Supplies the picture in a file.
    /// </param>
    public InstalledPets(IPetCatalog catalog, IPetLoader loader, string petsDirectory, Func<string, ImageSource?> frames)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentException.ThrowIfNullOrEmpty(petsDirectory);
        ArgumentNullException.ThrowIfNull(frames);

        _catalog = catalog;
        _loader = loader;
        _petsDirectory = petsDirectory;
        _frames = frames;
    }

    /// <summary>
    /// Raised when the pets that can be chosen are not the same as before, after <see cref="Entries"/> has changed and the
    /// refresh is over, so a handler may ask for another one. It is raised on the thread that asked for the refresh, and
    /// its handlers must not throw.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the pets that can be chosen, in the order of their folder names.
    /// </summary>
    public IReadOnlyList<InstalledPet> Entries { get; private set; } = [];

    /// <summary>
    /// Gets a number that changes whenever <see cref="Entries"/> does.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Looks for pets again. Pets that were loaded are kept as they are, the others are loaded.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the pets that can be chosen are not the same as before.
    /// </returns>
    public async Task<bool> RefreshAsync()
    {
        bool changed;
        await _refreshGate.WaitAsync();
        try
        {
            var known = Entries.ToDictionary(entry => entry.FolderName, StringComparer.Ordinal);
            var entries = new List<InstalledPet>();
            // Listing the folders reads the disk, which should not hold up the UI thread each time the menu opens.
            var names = await Task.Run(_catalog.GetPetNames);
            foreach (var name in names)
            {
                var entry = known.TryGetValue(name, out var existing) ? existing : await LoadEntryAsync(name);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            changed = !entries.Select(entry => entry.FolderName).SequenceEqual(Entries.Select(entry => entry.FolderName), StringComparer.Ordinal);
            if (changed)
            {
                Entries = entries;
                Version++;
            }
        }
        finally
        {
            _refreshGate.Release();
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    /// <summary>
    /// Loads a pet for the menu.
    /// </summary>
    /// <param name="name">
    /// The pet's folder name.
    /// </param>
    /// <returns>
    /// The entry, or <see langword="null"/> if the pet cannot be loaded; the panel shows such a pet with the reason.
    /// </returns>
    private async Task<InstalledPet?> LoadEntryAsync(string name)
    {
        try
        {
            var pet = await _loader.LoadAsync(Path.Combine(_petsDirectory, name));
            var path = pet.GetAnimation(PetState.Idle).FramePaths.FirstOrDefault();
            return new InstalledPet(name, pet.Name, path is null ? null : _frames(path), pet.FrameWidth, pet.FrameHeight);
        }
        catch (Exception ex) when (PetLoadFailure.Is(ex))
        {
            return null;
        }
    }
}
