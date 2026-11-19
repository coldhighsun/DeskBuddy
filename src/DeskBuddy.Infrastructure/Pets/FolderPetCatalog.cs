namespace DeskBuddy.Infrastructure.Pets;

/// <summary>
/// Lists the pets in one or more folders: every subfolder that contains a <c>manifest.json</c> is a pet. Whether the
/// manifest is valid is only found out when the pet is loaded. When a pet of the same name is in several folders, the
/// one in the folder listed first wins, so the user's own pets can stand in for the shipped ones.
/// </summary>
public sealed class FolderPetCatalog : IPetCatalog
{
    /// <summary>
    /// The folders that hold one subfolder per pet, the one that wins first.
    /// </summary>
    private readonly IReadOnlyList<string> _roots;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPetCatalog"/> class.
    /// </summary>
    /// <param name="roots">
    /// The folders that hold one subfolder per pet, in order of precedence. Folders that do not exist are skipped.
    /// </param>
    public FolderPetCatalog(params IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0)
        {
            throw new ArgumentException("At least one pets folder is needed.", nameof(roots));
        }

        _roots = roots;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetPetNames()
    {
        return
        [
            .. _roots
                .SelectMany(ListPets)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <inheritdoc />
    public string GetPetDirectory(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        foreach (var root in _roots)
        {
            var directory = Path.Combine(root, name);
            if (File.Exists(Path.Combine(directory, FolderPetLoader.ManifestFileName)))
            {
                return directory;
            }
        }

        // Not found anywhere: the folder it would have in the last (shipped) folder, where loading reports it missing.
        return Path.Combine(_roots[^1], name);
    }

    /// <summary>
    /// Lists the pets in one folder.
    /// </summary>
    /// <param name="root">
    /// The folder.
    /// </param>
    /// <returns>
    /// The names of its subfolders that hold a manifest; empty if the folder does not exist or cannot be read.
    /// </returns>
    private static IEnumerable<string> ListPets(string root)
    {
        try
        {
            return
            [
                .. Directory.EnumerateDirectories(root)
                    .Where(directory => File.Exists(Path.Combine(directory, FolderPetLoader.ManifestFileName)))
                    .Select(directory => Path.GetFileName(directory)),
            ];
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }
}
