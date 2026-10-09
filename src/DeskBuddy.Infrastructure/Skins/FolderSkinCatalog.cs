using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.Infrastructure.Skins;

/// <summary>
/// Lists the skins in a folder: every subfolder that contains a <c>manifest.json</c> is a skin. Whether the manifest is
/// valid is only found out when the skin is loaded.
/// </summary>
/// <param name="skinsDirectory">
/// The folder that holds one subfolder per skin.
/// </param>
public sealed class FolderSkinCatalog(string skinsDirectory) : ISkinCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<string> GetSkinNames()
    {
        try
        {
            return
            [
                .. Directory.EnumerateDirectories(skinsDirectory)
                    .Where(directory => File.Exists(Path.Combine(directory, FolderSkinLoader.ManifestFileName)))
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
