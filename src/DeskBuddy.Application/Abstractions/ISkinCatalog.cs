namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Lists the skins that can be chosen.
/// </summary>
public interface ISkinCatalog
{
    /// <summary>
    /// Lists the names of the installed skins. The name is also the name of the skin's folder, so it can be passed on
    /// to be loaded.
    /// </summary>
    /// <returns>
    /// The skin names, sorted; empty if there are none or the skins folder cannot be read.
    /// </returns>
    IReadOnlyList<string> GetSkinNames();
}
