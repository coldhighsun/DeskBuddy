namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Lists the pets that can be chosen.
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
}
