using AwesomeAssertions;
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

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// Creates a subfolder, with a manifest if asked to.
    /// </summary>
    /// <param name="name">
    /// The folder name.
    /// </param>
    /// <param name="withManifest">
    /// Whether to put a <c>manifest.json</c> in it.
    /// </param>
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
}
