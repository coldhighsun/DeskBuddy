using AwesomeAssertions;
using DeskBuddy.Domain;
using DeskBuddy.Infrastructure.Skins;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskBuddy.Infrastructure.Tests.Skins;

/// <summary>
/// Tests for <see cref="FolderSkinLoader"/>, using a real temporary directory per test.
/// </summary>
public sealed class FolderSkinLoaderTests : IDisposable
{
    /// <summary>
    /// Temporary skin directory, unique per test instance.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddyTests_{Guid.NewGuid():N}");

    /// <summary>
    /// The loader under test.
    /// </summary>
    private readonly FolderSkinLoader _loader = new(NullLogger<FolderSkinLoader>.Instance);

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderSkinLoaderTests"/> class and creates the temp directory.
    /// </summary>
    public FolderSkinLoaderTests()
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
    private void WriteSkin(string manifestJson, params string[] frames)
    {
        File.WriteAllText(Path.Combine(_directory, FolderSkinLoader.ManifestFileName), manifestJson);
        foreach (var frame in frames)
        {
            File.WriteAllBytes(Path.Combine(_directory, frame), []);
        }
    }

    /// <summary>
    /// A valid manifest yields a skin with resolved absolute frame paths.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ValidManifest_ReturnsSkinWithAbsoluteFramePaths()
    {
        // Arrange
        WriteSkin(
            """{"name":"cat","frameSize":[64,96],"states":{"Idle":{"fps":4,"frames":["a.png","b.png"]}}}""",
            "a.png",
            "b.png");

        // Act
        var skin = await _loader.LoadAsync(_directory);

        // Assert
        skin.Name.Should().Be("cat");
        skin.FrameWidth.Should().Be(64);
        skin.FrameHeight.Should().Be(96);
        skin.GetAnimation(PetState.Idle).Fps.Should().Be(4);
        skin.GetAnimation(PetState.Idle).FramePaths.Should().Equal(
            Path.Combine(_directory, "a.png"),
            Path.Combine(_directory, "b.png"));
    }

    /// <summary>
    /// State names in the manifest are matched case-insensitively.
    /// </summary>
    [Fact]
    public async Task LoadAsync_LowercaseStateName_IsRecognized()
    {
        // Arrange
        WriteSkin(
            """{"frameSize":[8,8],"states":{"idle":{"fps":1,"frames":["i.png"]},"walk":{"fps":1,"frames":["w.png"]}}}""",
            "i.png",
            "w.png");

        // Act
        var skin = await _loader.LoadAsync(_directory);

        // Assert
        skin.Animations.Should().ContainKey(PetState.Walk);
    }

    /// <summary>
    /// States the skin does not define fall back to Idle.
    /// </summary>
    [Fact]
    public async Task LoadAsync_MissingState_FallsBackToIdle()
    {
        // Arrange
        WriteSkin("""{"frameSize":[8,8],"states":{"Idle":{"fps":1,"frames":["i.png"]}}}""", "i.png");

        // Act
        var skin = await _loader.LoadAsync(_directory);

        // Assert
        skin.GetAnimation(PetState.Tired).Should().BeSameAs(skin.GetAnimation(PetState.Idle));
    }

    /// <summary>
    /// Unknown state names are skipped instead of failing the whole skin.
    /// </summary>
    [Fact]
    public async Task LoadAsync_UnknownStateName_IsIgnored()
    {
        // Arrange
        WriteSkin(
            """{"frameSize":[8,8],"states":{"Idle":{"fps":1,"frames":["i.png"]},"Dance":{"fps":1,"frames":["i.png"]}}}""",
            "i.png");

        // Act
        var skin = await _loader.LoadAsync(_directory);

        // Assert
        skin.Animations.Should().HaveCount(1);
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
        WriteSkin("{ not json");

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
        WriteSkin("""{"frameSize":[8,8],"states":{"Idle":{"fps":1,"frames":["missing.png"]}}}""");

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
        WriteSkin("""{"frameSize":[8,8],"states":{"Walk":{"fps":1,"frames":["w.png"]}}}""", "w.png");

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
        WriteSkin("""{"frameSize":[8,8],"states":{"Idle":{"fps":0,"frames":["i.png"]}}}""", "i.png");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }
}
