using System.IO;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="InstalledPets"/>.
/// </summary>
public class InstalledPetsTests
{
    /// <summary>
    /// The pets installed, which the tests change.
    /// </summary>
    private readonly FakePetCatalog _installed = new();

    /// <summary>
    /// The loader behind the list.
    /// </summary>
    private readonly FakePetLoader _loader = new();

    /// <summary>
    /// The paths the pictures were asked for.
    /// </summary>
    private readonly List<string> _pictureRequests = [];

    /// <summary>
    /// The list under test.
    /// </summary>
    private readonly InstalledPets _list;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstalledPetsTests"/> class.
    /// </summary>
    public InstalledPetsTests()
    {
        _list = new InstalledPets(
            _installed,
            _loader,
            "folder-without-pets",
            path =>
            {
                _pictureRequests.Add(path);
                return _picturesAvailable ? Picture : null;
            });
    }

    /// <summary>
    /// A picture to give for any file, as the pictures that were read.
    /// </summary>
    private static readonly System.Windows.Media.Imaging.BitmapSource Picture = MakePicture();

    /// <summary>
    /// Whether the pictures can be read: when not, the pet loads but has no picture, as with a corrupt atlas.
    /// </summary>
    private bool _picturesAvailable = true;

    /// <summary>
    /// Makes a small frozen picture.
    /// </summary>
    /// <returns>
    /// The picture.
    /// </returns>
    private static System.Windows.Media.Imaging.BitmapSource MakePicture()
    {
        var picture = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
        picture.Freeze();
        return picture;
    }

    /// <summary>
    /// The pets that load are listed, in the order of the catalog, with the name and frame size they declare.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_InstalledPets_ListsThemWithTheirDetails()
    {
        // Arrange
        _installed.Names = ["cat", "robot"];
        _loader.Available.Add("cat");
        _loader.Available.Add("robot");
        _loader.DisplayNames["robot"] = "Robot head";

        // Act
        var changed = await _list.RefreshAsync();

        // Assert
        changed.Should().BeTrue();
        _list.Entries.Select(entry => entry.FolderName).Should().Equal("cat", "robot");
        _list.Entries.Select(entry => entry.DisplayName).Should().Equal("cat", "Robot head");
        _list.Entries.Should().OnlyContain(entry => entry.FrameWidth == 128 && entry.FrameHeight == 128);
    }

    /// <summary>
    /// The picture of a pet is its first idle picture.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_Pet_AsksForItsIdlePicture()
    {
        // Arrange
        _installed.Names = ["cat"];
        _loader.Available.Add("cat");

        // Act
        await _list.RefreshAsync();

        // Assert
        _pictureRequests.Should().Equal("idle.png");
    }

    /// <summary>
    /// A pet that brings an <c>icon.png</c> has it as its icon picture; one that does not has none.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetWithIconFile_HasIconPicture()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), $"DeskBuddyIcons-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "cat"));
        Directory.CreateDirectory(Path.Combine(root, "robot"));
        await File.WriteAllBytesAsync(Path.Combine(root, "cat", "icon.png"), [0], TestContext.Current.CancellationToken);
        try
        {
            var requested = new List<string>();
            var list = new InstalledPets(
                new FakePetCatalog("cat", "robot"),
                new FakePetLoader { Available = { "cat", "robot" } },
                root,
                path =>
                {
                    requested.Add(path);
                    return System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
                });

            // Act
            await list.RefreshAsync();

            // Assert
            list.Entries.Single(entry => entry.FolderName == "cat").IconPicture.Should().NotBeNull();
            list.Entries.Single(entry => entry.FolderName == "robot").IconPicture.Should().BeNull();
            requested.Should().Contain(Path.Combine(root, "cat", InstalledPets.IconFileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The idle picture and the icon picture of a pet are asked to be cut out ahead of time, off the UI thread.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetWithIconFile_PreloadsIdleAndIconPictures()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), $"DeskBuddyIcons-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "cat"));
        await File.WriteAllBytesAsync(Path.Combine(root, "cat", "icon.png"), [0], TestContext.Current.CancellationToken);
        try
        {
            var preloaded = new List<string>();
            var list = new InstalledPets(
                new FakePetCatalog("cat"),
                new FakePetLoader { Available = { "cat" } },
                root,
                _ => null,
                names =>
                {
                    preloaded.AddRange(names);
                    return Task.CompletedTask;
                });

            // Act
            await list.RefreshAsync();

            // Assert
            preloaded.Should().Equal("idle.png", Path.Combine(root, "cat", InstalledPets.IconFileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A pet that loaded but whose picture could not be read is loaded again by the next refresh, and when the picture can be
    /// read then (the atlas has been repaired) the change is reported and the entry has its picture.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetWithoutPicture_IsLoadedAgainAndPickedUpOnceRepaired()
    {
        // Arrange
        _installed.Names = ["cat"];
        _loader.Available.Add("cat");
        _picturesAvailable = false;
        await _list.RefreshAsync();
        _list.Entries.Single().Picture.Should().BeNull();
        var versionBefore = _list.Version;
        _loader.Requested.Clear();
        _picturesAvailable = true;

        // Act
        var changed = await _list.RefreshAsync();

        // Assert
        changed.Should().BeTrue();
        _loader.Requested.Should().Equal("cat");
        _list.Entries.Single().Picture.Should().BeSameAs(Picture);
        _list.Version.Should().BeGreaterThan(versionBefore);
    }

    /// <summary>
    /// Loading one pet gives its entry without putting it in the list and without loading the others.
    /// </summary>
    [Fact]
    public async Task LoadAsync_OnePet_ReturnsItsEntryOnly()
    {
        // Arrange
        _installed.Names = ["cat", "robot"];
        _loader.Available.Add("cat");
        _loader.Available.Add("robot");

        // Act
        var entry = await _list.LoadAsync("robot");

        // Assert
        entry.Should().NotBeNull();
        entry!.FolderName.Should().Be("robot");
        _loader.Requested.Should().Equal("robot");
        _list.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// A pet that cannot be loaded gives no entry, rather than failing.
    /// </summary>
    [Fact]
    public async Task LoadAsync_BrokenPet_ReturnsNull()
    {
        // Arrange
        _installed.Names = ["broken"];

        // Act
        var entry = await _list.LoadAsync("broken");

        // Assert
        entry.Should().BeNull();
    }

    /// <summary>
    /// A pet that cannot be loaded is left out and does not stop the others from being listed.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_BrokenPet_IsLeftOut()
    {
        // Arrange
        _installed.Names = ["broken", "cat"];
        _loader.Available.Add("cat");

        // Act
        await _list.RefreshAsync();

        // Assert
        _list.Entries.Select(entry => entry.FolderName).Should().Equal("cat");
    }

    /// <summary>
    /// A pet that could not be loaded is tried again on the next refresh, so a repaired pet needs no restart.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_BrokenPetRepaired_AppearsOnNextRefresh()
    {
        // Arrange
        _installed.Names = ["cat"];
        await _list.RefreshAsync();

        // Act
        _loader.Available.Add("cat");
        var changed = await _list.RefreshAsync();

        // Assert
        changed.Should().BeTrue();
        _list.Entries.Select(entry => entry.FolderName).Should().Equal("cat");
    }

    /// <summary>
    /// A pet that was removed from the folder disappears from the list.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_PetRemoved_DisappearsFromList()
    {
        // Arrange
        _installed.Names = ["cat", "robot"];
        _loader.Available.Add("cat");
        _loader.Available.Add("robot");
        await _list.RefreshAsync();

        // Act
        _installed.Names = ["robot"];
        var changed = await _list.RefreshAsync();

        // Assert
        changed.Should().BeTrue();
        _list.Entries.Select(entry => entry.FolderName).Should().Equal("robot");
    }

    /// <summary>
    /// A refresh that finds the same pets reports no change, keeps the version and does not load the pets again.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_NothingChanged_ReportsNoChangeAndLoadsNothing()
    {
        // Arrange
        _installed.Names = ["cat"];
        _loader.Available.Add("cat");
        await _list.RefreshAsync();
        var version = _list.Version;
        _loader.Requested.Clear();

        // Act
        var changed = await _list.RefreshAsync();

        // Assert
        changed.Should().BeFalse();
        _list.Version.Should().Be(version);
        _loader.Requested.Should().BeEmpty();
    }

    /// <summary>
    /// The version goes up each time the pets that can be chosen change, and only then.
    /// </summary>
    [Fact]
    public async Task Version_AfterChanges_CountsThem()
    {
        // Arrange
        _installed.Names = ["cat"];
        _loader.Available.Add("cat");
        var initial = _list.Version;

        // Act
        await _list.RefreshAsync();
        await _list.RefreshAsync();
        _installed.Names = [];
        await _list.RefreshAsync();

        // Assert
        _list.Version.Should().Be(initial + 2);
    }

    /// <summary>
    /// Refreshes that overlap load a pet once, as one waits for the other.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_OverlappingRefreshes_LoadEachPetOnce()
    {
        // Arrange
        _installed.Names = ["cat"];
        _loader.Available.Add("cat");
        var gate = new TaskCompletionSource();
        _loader.Gates["cat"] = gate;

        // Act
        var first = _list.RefreshAsync();
        var second = _list.RefreshAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        _loader.Requested.Should().Equal("cat");
        _list.Entries.Should().ContainSingle();
    }
}
