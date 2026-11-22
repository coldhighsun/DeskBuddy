using System.IO;
using DeskBuddy.App.Rendering;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App;

/// <summary>
/// Loads the pet to show at start-up.
/// </summary>
internal static class StartupPet
{
    /// <summary>
    /// Loads the pet to show at start-up and decodes its pictures off the UI thread. A pet that is missing or broken
    /// is replaced by the default pet, so a bad pet folder does not stop the application from starting.
    /// </summary>
    /// <param name="loader">Reads a pet from its folder.</param>
    /// <param name="catalog">Tells where the folder of a pet is.</param>
    /// <param name="petName">The name of the pet in the settings.</param>
    /// <param name="logger">Reports a pet that had to be replaced.</param>
    /// <returns>The pet, its decoded pictures and the name of the pet actually loaded.</returns>
    /// <exception cref="Exception">
    /// An exception for which <see cref="PetLoadFailure.Is"/> is <see langword="true"/> when the default pet cannot be
    /// loaded either.
    /// </exception>
    public static async Task<(Pet Pet, PetFrameCache Frames, string Name)> LoadAsync(
        IPetLoader loader,
        IPetCatalog catalog,
        string petName,
        ILogger logger)
    {
        if (!string.Equals(petName, PetSettings.DefaultPetName, StringComparison.OrdinalIgnoreCase))
        {
            var petDirectory = catalog.GetPetDirectory(petName);
            if (Directory.Exists(petDirectory))
            {
                try
                {
                    var pet = await loader.LoadAsync(petDirectory);
                    return (pet, await PetFrameCache.CreateAsync(pet), petName);
                }
                catch (Exception ex) when (PetLoadFailure.Is(ex))
                {
                    logger.LogWarning(ex, "The '{Pet}' pet could not be loaded; using '{Default}'.", petName, PetSettings.DefaultPetName);
                }
            }
            else
            {
                logger.LogWarning("The '{Pet}' pet was not found; using '{Default}'.", petName, PetSettings.DefaultPetName);
            }
        }

        var defaultPet = await loader.LoadAsync(catalog.GetPetDirectory(PetSettings.DefaultPetName));
        return (defaultPet, await PetFrameCache.CreateAsync(defaultPet), PetSettings.DefaultPetName);
    }
}
