namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Lists the pets that can be chosen, and removes the ones the user put there.
/// </summary>
public interface IPetCatalog
{
    /// <summary>
    /// Lists the names of the installed pets. The name is also the name of the pet's folder, so it can be passed on
    /// to be loaded.
    /// </summary>
    /// <returns>The pet names, sorted; empty if there are none or the pets folder cannot be read.</returns>
    IReadOnlyList<string> GetPetNames();

    /// <summary>
    /// Gets the folder a pet is loaded from.
    /// </summary>
    /// <param name="name">
    /// The name of the pet, as listed by <see cref="GetPetNames"/> or stored in the settings.
    /// </param>
    /// <returns>
    /// The pet's folder. For a pet that is not installed, a folder that does not exist, so that loading it fails.
    /// </returns>
    string GetPetDirectory(string name);

    /// <summary>
    /// Tells whether a pet can be removed: only a pet in the user's own folder can, never one that comes with the
    /// application, as an update puts those back.
    /// </summary>
    /// <param name="name">The name of the pet.</param>
    /// <returns>
    /// <see langword="true"/> if the pet has a folder of its own in the user's pets folder. A pet that has one there and
    /// also comes with the application can be removed; it is the user's copy that goes.
    /// </returns>
    bool CanRemove(string name);

    /// <summary>
    /// Removes the user's copy of a pet: its whole folder, to the Recycle Bin unless told otherwise.
    /// </summary>
    /// <param name="name">The name of the pet.</param>
    /// <param name="permanently">
    /// <see langword="true"/> to delete the folder for good instead of moving it to the Recycle Bin.
    /// </param>
    /// <returns>What came of it; it does not throw for an error with the folder.</returns>
    Task<PetRemoval> RemoveAsync(string name, bool permanently = false);
}

/// <summary>
/// What came of removing a pet.
/// </summary>
public enum PetRemoval
{
    /// <summary>
    /// The pet's folder was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The pet cannot be removed: it has no folder of its own in the user's pets folder, or the name is no folder name.
    /// </summary>
    NotRemovable,

    /// <summary>
    /// The folder could not be moved to the Recycle Bin; asking again for a permanent removal may work.
    /// </summary>
    RecycleUnavailable,

    /// <summary>
    /// The folder could not be removed (it is in use, or access is denied).
    /// </summary>
    Failed,
}
