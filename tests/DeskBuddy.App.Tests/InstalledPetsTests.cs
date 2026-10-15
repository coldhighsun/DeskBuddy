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
            "pets",
            path =>
            {
                _pictureRequests.Add(path);
                return null;
            });
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
