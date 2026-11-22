using DeskBuddy.Infrastructure.Pets;

namespace DeskBuddy.Infrastructure.Tests.Pets;

/// <summary>
/// Tests for <see cref="FolderPetCatalog"/>, using a real temporary directory per test.
/// </summary>
public sealed class FolderPetCatalogTests : IDisposable
{
    /// <summary>
    /// Temporary pets directory, unique per test instance.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyPets_{Guid.NewGuid():N}");

    /// <summary>
    /// The catalog under test.
    /// </summary>
    private readonly FolderPetCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPetCatalogTests"/> class.
    /// </summary>
    public FolderPetCatalogTests()
    {
        _catalog = new FolderPetCatalog(_directory);
    }

    /// <summary>
    /// Gets a second pets folder, standing for the user's own, removed with the first.
    /// </summary>
    private string UserDirectory => _directory + "_user";

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var directory in new[] { _directory, UserDirectory })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Creates a subfolder, with a manifest if asked to.
    /// </summary>
    /// <param name="name">The folder name.</param>
    /// <param name="withManifest">Whether to put a <c>manifest.json</c> in it.</param>
    private void AddFolder(string name, bool withManifest)
    {
        var folder = Path.Combine(_directory, name);
        Directory.CreateDirectory(folder);
        if (withManifest)
        {
            File.WriteAllText(Path.Combine(folder, FolderPetLoader.ManifestFileName), "{}");
        }
    }

    /// <summary>
    /// A pets folder that does not exist yields no pets instead of an error.
    /// </summary>
    [Fact]
    public void GetPetNames_FolderMissing_ReturnsEmpty()
    {
        // Act
        var names = _catalog.GetPetNames();

        // Assert
        names.Should().BeEmpty();
    }

    /// <summary>
    /// Only folders with a manifest count as pets, and the names are sorted ignoring case.
    /// </summary>
    [Fact]
    public void GetPetNames_MixedFolders_ListsOnlyPetsSorted()
    {
        // Arrange
        AddFolder("zebra", withManifest: true);
        AddFolder("Alpha", withManifest: true);
        AddFolder("notes", withManifest: false);
        File.WriteAllText(Path.Combine(_directory, FolderPetLoader.ManifestFileName), "{}");

        // Act
        var names = _catalog.GetPetNames();

        // Assert
        names.Should().Equal("Alpha", "zebra");
    }

    /// <summary>
    /// The pets of every folder are listed, each name once.
    /// </summary>
    [Fact]
    public void GetPetNames_SeveralFolders_ListsPetsOfAllOnce()
    {
        // Arrange
        AddFolder("robot", withManifest: true);
        AddFolder("cat", withManifest: true);
        AddPet(UserDirectory, "Robot");
        AddPet(UserDirectory, "mine");
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var names = catalog.GetPetNames();

        // Assert
        names.Should().HaveCount(3);
        names.Should().Contain(["cat", "mine"]);
    }

    /// <summary>
    /// A pet in the folder listed first wins over one of the same name in a later folder.
    /// </summary>
    [Fact]
    public void GetPetDirectory_PetInFirstFolder_ReturnsIt()
    {
        // Arrange
        AddFolder("robot", withManifest: true);
        AddPet(UserDirectory, "robot");
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var directory = catalog.GetPetDirectory("robot");

        // Assert
        directory.Should().Be(Path.Combine(UserDirectory, "robot"));
    }

    /// <summary>
    /// A pet only in a later folder is found there; a folder without a manifest does not count.
    /// </summary>
    [Fact]
    public void GetPetDirectory_PetOnlyInLaterFolder_ReturnsThatFolder()
    {
        // Arrange
        AddFolder("cat", withManifest: true);
        Directory.CreateDirectory(Path.Combine(UserDirectory, "cat"));
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var directory = catalog.GetPetDirectory("cat");

        // Assert
        directory.Should().Be(Path.Combine(_directory, "cat"));
    }

    /// <summary>
    /// A pet that is nowhere gets a folder in the last folder, where loading it fails.
    /// </summary>
    [Fact]
    public void GetPetDirectory_UnknownPet_ReturnsFolderInLastFolder()
    {
        // Arrange
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var directory = catalog.GetPetDirectory("ghost");

        // Assert
        directory.Should().Be(Path.Combine(_directory, "ghost"));
    }

    /// <summary>
    /// Creates a pet folder with a manifest under a given folder.
    /// </summary>
    /// <param name="root">The folder of pets.</param>
    /// <param name="name">The pet's folder name.</param>
    private static void AddPet(string root, string name)
    {
        var folder = Path.Combine(root, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FolderPetLoader.ManifestFileName), "{}");
    }
}
