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
    /// <returns>
    /// The pet names, sorted; empty if there are none or the pets folder cannot be read.
    /// </returns>
    IReadOnlyList<string> GetPetNames();
}
