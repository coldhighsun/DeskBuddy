using DeskBuddy.Domain;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Infrastructure.Pets;

namespace DeskBuddy.Infrastructure.Tests.Pets;

/// <summary>
/// Tests for <see cref="FolderPetLoader"/>, using a real temporary directory per test.
/// </summary>
public sealed class FolderPetLoaderTests : IDisposable
{
    /// <summary>
    /// Temporary pet directory, unique per test instance.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyTests_{Guid.NewGuid():N}");

    /// <summary>
    /// The loader under test.
    /// </summary>
    private readonly FolderPetLoader _loader = new(NullLogger<FolderPetLoader>.Instance);

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPetLoaderTests"/> class and creates the temp directory.
    /// </summary>
    public FolderPetLoaderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    /// <summary>
    /// Writes a manifest and empty frame files into the temp directory.
    /// </summary>
    /// <param name="manifestJson">
    /// Manifest content.
    /// </param>
    /// <param name="frames">
    /// Names of frame files to create.
    /// </param>
    private void WritePet(string manifestJson, params string[] frames)
    {
        File.WriteAllText(Path.Combine(_directory, FolderPetLoader.ManifestFileName), manifestJson);
        foreach (var frame in frames)
        {
            File.WriteAllBytes(Path.Combine(_directory, frame), []);
        }
    }

    /// <summary>
    /// A valid manifest yields a pet with resolved absolute frame paths.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ValidManifest_ReturnsPetWithAbsoluteFramePaths()
    {
        // Arrange
        WritePet(
            """{"name":"cat","frameSize":[64,96],"states":{"Idle":{"fps":4,"frames":["a.png","b.png"]}}}""",
            "a.png",
            "b.png");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.Name.Should().Be("cat");
        pet.FrameWidth.Should().Be(64);
        pet.FrameHeight.Should().Be(96);
        pet.GetAnimation(PetState.Idle).Fps.Should().Be(4);
        pet.GetAnimation(PetState.Idle).FramePaths.Should().Equal(
            Path.Combine(_directory, "a.png"),
            Path.Combine(_directory, "b.png"));
    }

    /// <summary>
    /// Entries named Idle.Left and the like become looks of the Idle state, not states of their own.
    /// </summary>
    [Fact]
    public async Task LoadAsync_IdleLookEntries_AreLoadedAsGazeAnimations()
    {
        // Arrange
        WritePet(
            """{"frameSize":[8,8],"states":{"Idle":{"fps":1,"frames":["i.png"]},"Idle.Left":{"fps":1,"frames":["l.png"]},"idle.up":{"fps":1,"frames":["u.png"]},"Idle.Sideways":{"fps":1,"frames":["s.png"]}}}""",
            "i.png",
            "l.png",
            "u.png",
            "s.png");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.GazeAnimations.Keys.Should().BeEquivalentTo([GazeDirection.Left, GazeDirection.Up]);
        pet.GetAnimation(PetState.Idle, GazeDirection.Left).FramePaths.Should().Equal(Path.Combine(_directory, "l.png"));
        pet.Animations.Keys.Should().BeEquivalentTo([PetState.Idle]);
    }

    /// <summary>
    /// State names in the manifest are matched case-insensitively.
    /// </summary>
    [Fact]
    public async Task LoadAsync_LowercaseStateName_IsRecognized()
    {
        // Arrange
        WritePet(
            """{"frameSize":[8,8],"states":{"idle":{"fps":1,"frames":["i.png"]},"walk":{"fps":1,"frames":["w.png"]}}}""",
            "i.png",
            "w.png");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.Animations.Should().ContainKey(PetState.Walk);
    }

    /// <summary>
    /// States the pet does not define fall back to Idle.
    /// </summary>
    [Fact]
    public async Task LoadAsync_MissingState_FallsBackToIdle()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],"states":{"Idle":{"fps":1,"frames":["i.png"]}}}""", "i.png");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.GetAnimation(PetState.Tired).Should().BeSameAs(pet.GetAnimation(PetState.Idle));
    }

    /// <summary>
    /// Unknown state names are skipped instead of failing the whole pet.
    /// </summary>
    [Fact]
    public async Task LoadAsync_UnknownStateName_IsIgnored()
    {
        // Arrange
        WritePet(
            """{"frameSize":[8,8],"states":{"Idle":{"fps":1,"frames":["i.png"]},"Dance":{"fps":1,"frames":["i.png"]}}}""",
            "i.png");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.Animations.Should().HaveCount(1);
    }

    /// <summary>
    /// A directory without a manifest is rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_MissingManifest_ThrowsInvalidData()
    {
        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// Malformed JSON is reported as invalid data.
    /// </summary>
    [Fact]
    public async Task LoadAsync_MalformedJson_ThrowsInvalidData()
    {
        // Arrange
        WritePet("{ not json");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// A frame listed in the manifest but absent on disk is rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_FrameFileMissing_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],"states":{"Idle":{"fps":1,"frames":["missing.png"]}}}""");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// A manifest without an Idle animation is rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NoIdleState_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],"states":{"Walk":{"fps":1,"frames":["w.png"]}}}""", "w.png");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// A non-positive fps is rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ZeroFps_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],"states":{"Idle":{"fps":0,"frames":["i.png"]}}}""", "i.png");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }
}
