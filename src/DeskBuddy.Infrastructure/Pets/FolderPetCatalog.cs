using DeskBuddy.Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic.FileIO;

namespace DeskBuddy.Infrastructure.Pets;

/// <summary>
/// Lists the pets in one or more folders: every subfolder that contains a <c>manifest.json</c> is a pet. Whether the
/// manifest is valid is only found out when the pet is loaded. When a pet of the same name is in several folders, the
/// one in the folder listed first wins, so the user's own pets can stand in for the shipped ones. Only the pets in the
/// user's folder can be removed.
/// </summary>
public sealed class FolderPetCatalog : IPetCatalog
{
    /// <summary>
    /// The folders that hold one subfolder per pet, the one that wins first.
    /// </summary>
    private readonly IReadOnlyList<string> _roots;

    /// <summary>
    /// The folder of the user's own pets, whose pets can be removed; <see langword="null"/> if none can be.
    /// </summary>
    private readonly string? _userRoot;

    /// <summary>
    /// Moves a folder to the Recycle Bin; replaced in tests.
    /// </summary>
    private readonly Action<string> _recycle;

    /// <summary>
    /// Deletes a folder and what is in it for good; replaced in tests.
    /// </summary>
    private readonly Action<string> _delete;

    /// <summary>
    /// Reports why a pet could not be removed.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPetCatalog"/> class whose pets cannot be removed.
    /// </summary>
    /// <param name="roots">
    /// The folders that hold one subfolder per pet, in order of precedence. Folders that do not exist are skipped.
    /// </param>
    public FolderPetCatalog(params IReadOnlyList<string> roots)
        : this(roots, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPetCatalog"/> class for the user's folder and the one the pets
    /// that come with the application are in. The user's pets come first, so they can stand in for the shipped ones, and
    /// are the only ones that can be removed.
    /// </summary>
    /// <param name="userRoot">The folder of the user's own pets.</param>
    /// <param name="shippedRoot">The folder of the pets that come with the application.</param>
    /// <param name="logger">Reports why a pet could not be removed; <see langword="null"/> reports nothing.</param>
    public FolderPetCatalog(string userRoot, string shippedRoot, ILogger? logger = null)
        : this([userRoot, shippedRoot], userRoot, null, null, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPetCatalog"/> class that removes folders in its own way.
    /// </summary>
    /// <param name="roots">The folders that hold one subfolder per pet, in order of precedence.</param>
    /// <param name="userRoot">The folder of the pets that can be removed, or <see langword="null"/> if there is none.</param>
    /// <param name="recycle">Moves a folder to the Recycle Bin, or <see langword="null"/> for the real one.</param>
    /// <param name="delete">Deletes a folder for good, or <see langword="null"/> for the real one.</param>
    /// <param name="logger">Reports why a pet could not be removed; <see langword="null"/> reports nothing.</param>
    internal FolderPetCatalog(
        IReadOnlyList<string> roots,
        string? userRoot,
        Action<string>? recycle,
        Action<string>? delete,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0)
        {
            throw new ArgumentException("At least one pets folder is needed.", nameof(roots));
        }

        _roots = roots;
        _userRoot = userRoot;
        _recycle = recycle ?? MoveToRecycleBin;
        _delete = delete ?? DeleteForGood;
        _logger = logger ?? NullLogger.Instance;
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

    /// <inheritdoc />
    public bool CanRemove(string name)
    {
        return FindUserDirectory(name) is not null;
    }

    /// <inheritdoc />
    public Task<PetRemoval> RemoveAsync(string name, bool permanently = false)
    {
        return Task.Run(() => Remove(name, permanently));
    }

    /// <summary>
    /// Removes the user's copy of a pet.
    /// </summary>
    /// <param name="name">The name of the pet.</param>
    /// <param name="permanently">Whether to delete the folder for good instead of recycling it.</param>
    /// <returns>What came of it.</returns>
    private PetRemoval Remove(string name, bool permanently)
    {
        if (FindUserDirectory(name) is not { } directory)
        {
            return PetRemoval.NotRemovable;
        }

        try
        {
            if (new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                // A link to a folder elsewhere: only the link goes, never what it points to.
                Directory.Delete(directory, recursive: false);
                return PetRemoval.Removed;
            }

            if (permanently)
            {
                _delete(directory);
            }
            else
            {
                _recycle(directory);
            }

            return PetRemoval.Removed;
        }
        catch (Exception ex) when (!permanently && ex is IOException or OperationCanceledException or PlatformNotSupportedException)
        {
            // The caller may ask again for a permanent removal.
            _logger.LogWarning(ex, "The pet folder {Folder} could not be moved to the Recycle Bin.", directory);
            return PetRemoval.RecycleUnavailable;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _logger.LogWarning(ex, "The pet folder {Folder} could not be deleted.", directory);
            return PetRemoval.Failed;
        }
    }

    /// <summary>
    /// Finds the folder of the user's copy of a pet.
    /// </summary>
    /// <param name="name">The name of the pet.</param>
    /// <returns>
    /// The folder, or <see langword="null"/> if there is no user's folder, the name is no plain folder name, or the user
    /// has no pet of that name.
    /// </returns>
    private string? FindUserDirectory(string name)
    {
        if (_userRoot is null || !PetSettings.IsPlainFolderName(name))
        {
            return null;
        }

        var directory = Path.Combine(_userRoot, name);
        return File.Exists(Path.Combine(directory, FolderPetLoader.ManifestFileName)) ? directory : null;
    }

    /// <summary>
    /// Moves a folder and what is in it to the Recycle Bin.
    /// </summary>
    /// <param name="directory">The folder.</param>
    private static void MoveToRecycleBin(string directory)
    {
        FileSystem.DeleteDirectory(directory, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
    }

    /// <summary>
    /// Deletes a folder and what is in it for good.
    /// </summary>
    /// <param name="directory">The folder.</param>
    private static void DeleteForGood(string directory)
    {
        Directory.Delete(directory, recursive: true);
    }

    /// <summary>
    /// Lists the pets in one folder.
    /// </summary>
    /// <param name="root">The folder.</param>
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
