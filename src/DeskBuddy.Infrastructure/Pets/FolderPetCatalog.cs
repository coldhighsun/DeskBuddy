using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.Infrastructure.Pets;

/// <summary>
/// Lists the pets in a folder: every subfolder that contains a <c>manifest.json</c> is a pet. Whether the manifest is
/// valid is only found out when the pet is loaded.
/// </summary>
/// <param name="petsDirectory">
/// The folder that holds one subfolder per pet.
/// </param>
public sealed class FolderPetCatalog(string petsDirectory) : IPetCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<string> GetPetNames()
    {
        try
        {
            return
            [
                .. Directory.EnumerateDirectories(petsDirectory)
                    .Where(directory => File.Exists(Path.Combine(directory, FolderPetLoader.ManifestFileName)))
                    .Select(directory => Path.GetFileName(directory))
                    .Order(StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }
}
