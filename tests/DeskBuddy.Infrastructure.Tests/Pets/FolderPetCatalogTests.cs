using DeskBuddy.Application.Abstractions;
using DeskBuddy.Infrastructure.Pets;
using Microsoft.Extensions.Logging;

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
    /// Makes a catalog whose user folder is <see cref="UserDirectory"/> and whose shipped folder is the test folder, with
    /// removal done by recording the folder instead of deleting it.
    /// </summary>
    /// <param name="recycled">Receives the folders that were recycled.</param>
    /// <param name="deleted">Receives the folders that were deleted for good.</param>
    /// <param name="recycleFails">Whether recycling fails with an I/O error.</param>
    /// <returns>The catalog.</returns>
    private FolderPetCatalog RecordingCatalog(List<string> recycled, List<string> deleted, bool recycleFails = false)
    {
        return new FolderPetCatalog(
            [UserDirectory, _directory],
            UserDirectory,
            folder =>
            {
                if (recycleFails)
                {
                    throw new IOException("no Recycle Bin");
                }

                recycled.Add(folder);
            },
            deleted.Add);
    }

    /// <summary>
    /// Only a pet that has a folder of its own in the user's folder can be removed; one that only comes with the
    /// application cannot.
    /// </summary>
    [Fact]
    public void CanRemove_UsersPetAndShippedPet_OnlyTheUsersCan()
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        AddFolder("shipped", withManifest: true);
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act and assert
        catalog.CanRemove("mine").Should().BeTrue();
        catalog.CanRemove("shipped").Should().BeFalse();
        catalog.CanRemove("ghost").Should().BeFalse();
    }

    /// <summary>
    /// A pet the user has put in their folder under the name of a shipped pet can be removed: it is the user's copy that
    /// goes.
    /// </summary>
    [Fact]
    public void CanRemove_UsersCopyOfAShippedPet_IsTrue()
    {
        // Arrange
        AddFolder("robot", withManifest: true);
        AddPet(UserDirectory, "robot");
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var result = catalog.CanRemove("robot");

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// A catalog that was not told where the user's pets are removes nothing.
    /// </summary>
    [Fact]
    public void CanRemove_CatalogWithoutUserFolder_IsFalse()
    {
        // Arrange
        AddFolder("cat", withManifest: true);

        // Act
        var result = _catalog.CanRemove("cat");

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// A name that is no plain folder name is never removable, so a path cannot reach a folder outside the user's folder.
    /// </summary>
    /// <param name="name">The name.</param>
    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("..\\mine")]
    [InlineData("../mine")]
    [InlineData("a/b")]
    [InlineData("C:\\Windows")]
    public void CanRemove_NameThatIsNoFolderName_IsFalse(string name)
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var result = catalog.CanRemove(name);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Removing a pet moves the user's folder to the Recycle Bin and nothing else.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_UsersPet_RecyclesItsFolder()
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        AddFolder("shipped", withManifest: true);
        List<string> recycled = [];
        List<string> deleted = [];
        var catalog = RecordingCatalog(recycled, deleted);

        // Act
        var result = await catalog.RemoveAsync("mine");

        // Assert
        result.Should().Be(PetRemoval.Removed);
        recycled.Should().Equal(Path.Combine(UserDirectory, "mine"));
        deleted.Should().BeEmpty();
    }

    /// <summary>
    /// A permanent removal deletes the folder for good, without going through the Recycle Bin.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_Permanently_DeletesTheFolder()
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        List<string> recycled = [];
        List<string> deleted = [];
        var catalog = RecordingCatalog(recycled, deleted);

        // Act
        var result = await catalog.RemoveAsync("mine", permanently: true);

        // Assert
        result.Should().Be(PetRemoval.Removed);
        deleted.Should().Equal(Path.Combine(UserDirectory, "mine"));
        recycled.Should().BeEmpty();
    }

    /// <summary>
    /// A pet that comes with the application is not removed, and its folder is left alone.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_ShippedPet_IsNotRemovable()
    {
        // Arrange
        AddFolder("shipped", withManifest: true);
        List<string> recycled = [];
        List<string> deleted = [];
        var catalog = RecordingCatalog(recycled, deleted);

        // Act
        var result = await catalog.RemoveAsync("shipped");

        // Assert
        result.Should().Be(PetRemoval.NotRemovable);
        recycled.Should().BeEmpty();
        deleted.Should().BeEmpty();
        Directory.Exists(Path.Combine(_directory, "shipped")).Should().BeTrue();
    }

    /// <summary>
    /// With the same name in both folders only the user's copy is removed, and the shipped pet shows again afterwards.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_NameInBothFolders_RemovesOnlyTheUsersCopy()
    {
        // Arrange
        AddFolder("robot", withManifest: true);
        AddPet(UserDirectory, "robot");
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var result = await catalog.RemoveAsync("robot", permanently: true);

        // Assert
        result.Should().Be(PetRemoval.Removed);
        Directory.Exists(Path.Combine(UserDirectory, "robot")).Should().BeFalse();
        catalog.GetPetNames().Should().Equal("robot");
        catalog.GetPetDirectory("robot").Should().Be(Path.Combine(_directory, "robot"));
        catalog.CanRemove("robot").Should().BeFalse();
    }

    /// <summary>
    /// A permanent removal of a real folder deletes it with everything in it, and the pet is gone from the list.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_RealFolderPermanently_DeletesItWithItsContent()
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        File.WriteAllText(Path.Combine(UserDirectory, "mine", "atlas.png"), "not really a picture");
        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var result = await catalog.RemoveAsync("mine", permanently: true);

        // Assert
        result.Should().Be(PetRemoval.Removed);
        Directory.Exists(Path.Combine(UserDirectory, "mine")).Should().BeFalse();
        catalog.GetPetNames().Should().BeEmpty();
    }

    /// <summary>
    /// A name that points outside the user's folder is refused, and nothing is touched.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_NameWithPath_IsNotRemovable()
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        AddFolder("shipped", withManifest: true);
        List<string> recycled = [];
        List<string> deleted = [];
        var catalog = RecordingCatalog(recycled, deleted);

        // Act
        var result = await catalog.RemoveAsync("..\\" + Path.GetFileName(_directory) + "\\shipped", permanently: true);

        // Assert
        result.Should().Be(PetRemoval.NotRemovable);
        deleted.Should().BeEmpty();
        Directory.Exists(Path.Combine(_directory, "shipped")).Should().BeTrue();
    }

    /// <summary>
    /// A pet that is not there is not removable.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_UnknownPet_IsNotRemovable()
    {
        // Arrange
        var catalog = RecordingCatalog([], []);

        // Act
        var result = await catalog.RemoveAsync("ghost");

        // Assert
        result.Should().Be(PetRemoval.NotRemovable);
    }

    /// <summary>
    /// When the Recycle Bin cannot be used the answer says so, so that the caller can ask about deleting for good.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_RecycleFails_ReportsRecycleUnavailable()
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        List<string> deleted = [];
        var catalog = RecordingCatalog([], deleted, recycleFails: true);

        // Act
        var result = await catalog.RemoveAsync("mine");

        // Assert
        result.Should().Be(PetRemoval.RecycleUnavailable);
        deleted.Should().BeEmpty();
    }

    /// <summary>
    /// A folder that cannot be deleted (it is in use) is reported as failed, not as an exception.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_DeleteFails_ReportsFailed()
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        var catalog = new FolderPetCatalog(
            [UserDirectory, _directory],
            UserDirectory,
            null,
            _ => throw new UnauthorizedAccessException("denied"));

        // Act
        var result = await catalog.RemoveAsync("mine", permanently: true);

        // Assert
        result.Should().Be(PetRemoval.Failed);
    }

    /// <summary>
    /// The reason a folder could not be removed goes to the log, as the caller can only say that it failed.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_DeleteFails_LogsTheReason()
    {
        // Arrange
        AddPet(UserDirectory, "mine");
        var logger = new CapturingLogger();
        var catalog = new FolderPetCatalog(
            [UserDirectory, _directory],
            UserDirectory,
            null,
            _ => throw new UnauthorizedAccessException("denied"),
            logger);

        // Act
        await catalog.RemoveAsync("mine", permanently: true);

        // Assert
        logger.Warnings.Should().ContainSingle().Which.Should().BeOfType<UnauthorizedAccessException>();
    }

    /// <summary>
    /// A folder that is a link to a folder elsewhere is removed as a link: what it points to is never touched.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_FolderIsALink_RemovesOnlyTheLink()
    {
        // Arrange
        var target = Path.Combine(_directory, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, FolderPetLoader.ManifestFileName), "{}");
        File.WriteAllText(Path.Combine(target, "keep.txt"), "keep me");
        Directory.CreateDirectory(UserDirectory);
        var link = Path.Combine(UserDirectory, "linked");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Creating a link needs a privilege some machines do not give; there is nothing to test then.
            return;
        }

        var catalog = new FolderPetCatalog(UserDirectory, _directory);

        // Act
        var result = await catalog.RemoveAsync("linked", permanently: true);

        // Assert
        result.Should().Be(PetRemoval.Removed);
        Directory.Exists(link).Should().BeFalse();
        File.Exists(Path.Combine(target, "keep.txt")).Should().BeTrue();
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

    /// <summary>
    /// A logger that keeps the exceptions of the warnings it was given.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        /// <summary>
        /// Gets the exceptions that came with the warnings, in order.
        /// </summary>
        public List<Exception?> Warnings { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(exception);
            }
        }
    }
}
