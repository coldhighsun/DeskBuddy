using AwesomeAssertions;
using DeskBuddy.Infrastructure.Skins;

namespace DeskBuddy.Infrastructure.Tests.Skins;

/// <summary>
/// Tests for <see cref="FolderSkinCatalog"/>, using a real temporary directory per test.
/// </summary>
public sealed class FolderSkinCatalogTests : IDisposable
{
    /// <summary>
    /// Temporary skins directory, unique per test instance.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddySkins_{Guid.NewGuid():N}");

    /// <summary>
    /// The catalog under test.
    /// </summary>
    private readonly FolderSkinCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderSkinCatalogTests"/> class.
    /// </summary>
    public FolderSkinCatalogTests()
    {
        _catalog = new FolderSkinCatalog(_directory);
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
            File.WriteAllText(Path.Combine(folder, FolderSkinLoader.ManifestFileName), "{}");
        }
    }

    /// <summary>
    /// A skins folder that does not exist yields no skins instead of an error.
    /// </summary>
    [Fact]
    public void GetSkinNames_FolderMissing_ReturnsEmpty()
    {
        // Act
        var names = _catalog.GetSkinNames();

        // Assert
        names.Should().BeEmpty();
    }

    /// <summary>
    /// Only folders with a manifest count as skins, and the names are sorted ignoring case.
    /// </summary>
    [Fact]
    public void GetSkinNames_MixedFolders_ListsOnlySkinsSorted()
    {
        // Arrange
        AddFolder("zebra", withManifest: true);
        AddFolder("Alpha", withManifest: true);
        AddFolder("notes", withManifest: false);
        File.WriteAllText(Path.Combine(_directory, FolderSkinLoader.ManifestFileName), "{}");

        // Act
        var names = _catalog.GetSkinNames();

        // Assert
        names.Should().Equal("Alpha", "zebra");
    }
}
