using System.Buffers.Binary;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Infrastructure.Pets;

namespace DeskBuddy.Infrastructure.Tests.Pets;

/// <summary>
/// Tests for <see cref="FolderPetLoader"/>, using a real temporary directory per test.
/// </summary>
public sealed class FolderPetLoaderTests : IDisposable
{
    /// <summary>
    /// The atlas section the tests use unless they say otherwise: 4 columns of 8x8 cells, in an image of 32x16 pixels, so
    /// there are 8 cells.
    /// </summary>
    private const string Atlas = "\"atlas\":{\"image\":\"atlas.png\",\"cell\":[8,8],\"columns\":4}";

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
    /// Writes a manifest into the temp directory, and an atlas image of the given size next to it.
    /// </summary>
    /// <param name="manifestJson">
    /// Manifest content, in which <c>%ATLAS%</c> stands for the atlas section.
    /// </param>
    /// <param name="atlasWidth">
    /// Width of the atlas image in pixels, or 0 for no atlas file.
    /// </param>
    /// <param name="atlasHeight">
    /// Height of the atlas image in pixels.
    /// </param>
    private void WritePet(string manifestJson, int atlasWidth = 32, int atlasHeight = 16)
    {
        File.WriteAllText(Path.Combine(_directory, FolderPetLoader.ManifestFileName), manifestJson.Replace("%ATLAS%", Atlas));
        if (atlasWidth > 0)
        {
            File.WriteAllBytes(Path.Combine(_directory, "atlas.png"), PngHeader(atlasWidth, atlasHeight));
        }
    }

    /// <summary>
    /// Makes the start of a PNG file with the given size: the signature and the header chunk, which is all the loader reads.
    /// </summary>
    /// <param name="width">
    /// The width in pixels.
    /// </param>
    /// <param name="height">
    /// The height in pixels.
    /// </param>
    /// <returns>
    /// The bytes.
    /// </returns>
    private static byte[] PngHeader(int width, int height)
    {
        var bytes = new byte[33];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    /// <summary>
    /// The name a frame has in an atlas of the temp directory.
    /// </summary>
    /// <param name="x">
    /// The left edge of the cell in pixels.
    /// </param>
    /// <param name="y">
    /// The top edge of the cell in pixels.
    /// </param>
    /// <returns>
    /// The name of the frame.
    /// </returns>
    private string Cell(int x, int y)
    {
        return FrameId.Create(Path.Combine(_directory, "atlas.png"), x, y, 8, 8);
    }

    /// <summary>
    /// A valid manifest yields a pet whose frames are the cells of the atlas, counted along the rows.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ValidManifest_ReturnsPetWithFramesAsAtlasCells()
    {
        // Arrange
        WritePet("""{"name":"cat","frameSize":[64,64],%ATLAS%,"states":{"Idle":{"fps":4,"frames":[0,5,7]}}}""");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.Name.Should().Be("cat");
        pet.FrameWidth.Should().Be(64);
        pet.FrameHeight.Should().Be(64);
        pet.GetAnimation(PetState.Idle).Fps.Should().Be(4);
        pet.GetAnimation(PetState.Idle).FrameIds.Should().Equal(Cell(0, 0), Cell(8, 8), Cell(24, 8));
    }

    /// <summary>
    /// Without <c>columns</c> the atlas has as many columns as fit in the width of the image.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NoColumns_UsesAsManyAsFit()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],"atlas":{"image":"atlas.png","cell":[8,8]},"states":{"Idle":{"fps":1,"frames":[4]}}}""");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.GetAnimation(PetState.Idle).FrameIds.Should().Equal(Cell(0, 8));
    }

    /// <summary>
    /// Entries named Idle.Left and the like become looks of the Idle state, not states of their own.
    /// </summary>
    [Fact]
    public async Task LoadAsync_IdleLookEntries_AreLoadedAsGazeAnimations()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]},"Idle.Left":{"fps":1,"frames":[1]},"idle.up":{"fps":1,"frames":[2]},"Idle.Sideways":{"fps":1,"frames":[3]}}}""");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.GazeAnimations.Keys.Should().BeEquivalentTo([GazeDirection.Left, GazeDirection.Up]);
        pet.GetAnimation(PetState.Idle, GazeDirection.Left).FrameIds.Should().Equal(Cell(8, 0));
        pet.Animations.Keys.Should().BeEquivalentTo([PetState.Idle]);
    }

    /// <summary>
    /// State names in the manifest are matched case-insensitively.
    /// </summary>
    [Fact]
    public async Task LoadAsync_LowercaseStateName_IsRecognized()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"idle":{"fps":1,"frames":[0]},"walk":{"fps":1,"frames":[1]}}}""");

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
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]}}}""");

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
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]},"Dance":{"fps":1,"frames":[0]}}}""");

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
    /// Frames given as file names, the way pets were once written, are rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_FramesAsFileNames_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":["idle_0.png"]}}}""");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        (await act.Should().ThrowAsync<InvalidDataException>()).Which.Message.Should().Contain("atlas cell numbers");
    }

    /// <summary>
    /// An atlas cell whose proportions differ from the frame size is rejected, as the pet would be drawn squashed.
    /// </summary>
    [Fact]
    public async Task LoadAsync_CellProportionsDifferFromFrameSize_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[64,32],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]}}}""");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        (await act.Should().ThrowAsync<InvalidDataException>()).Which.Message.Should().Contain("proportions");
    }

    /// <summary>
    /// A cell of a different size from the frame size but the same proportions (the usual case: a frame drawn at twice the
    /// size it is shown at) is accepted.
    /// </summary>
    [Fact]
    public async Task LoadAsync_CellIsAMultipleOfFrameSize_Loads()
    {
        // Arrange
        WritePet("""{"frameSize":[4,4],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]}}}""");

        // Act
        var pet = await _loader.LoadAsync(_directory, TestContext.Current.CancellationToken);

        // Assert
        pet.FrameWidth.Should().Be(4);
    }

    /// <summary>
    /// A manifest without an atlas section is rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NoAtlasSection_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],"states":{"Idle":{"fps":1,"frames":[0]}}}""");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// An atlas image that is not there is rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_AtlasImageMissing_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]}}}""", atlasWidth: 0);

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// An atlas image that is not a PNG is rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_AtlasNotAPng_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]}}}""");
        File.WriteAllBytes(Path.Combine(_directory, "atlas.png"), new byte[40]);

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// An atlas file that ends inside the PNG header, or whose first chunk is not the header, is rejected.
    /// </summary>
    /// <param name="length">
    /// How many bytes of a valid header are kept.
    /// </param>
    /// <param name="corruptChunkName">
    /// Whether the name of the header chunk is spoiled.
    /// </param>
    [Theory]
    [InlineData(10, false)]
    [InlineData(23, false)]
    [InlineData(33, true)]
    public async Task LoadAsync_AtlasWithBrokenPngHeader_ThrowsInvalidData(int length, bool corruptChunkName)
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]}}}""");
        var header = PngHeader(32, 16);
        if (corruptChunkName)
        {
            header[12] = (byte)'X';
        }

        File.WriteAllBytes(Path.Combine(_directory, "atlas.png"), header[..length]);

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// An atlas image too narrow for the columns the manifest declares is rejected.
    /// </summary>
    [Fact]
    public async Task LoadAsync_AtlasTooNarrowForColumns_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[0]}}}""", atlasWidth: 24);

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// A columns value so large that columns times cell width overflows an int is rejected, instead of wrapping around to
    /// something that passes the size check.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ColumnsThatOverflowTheSizeCheck_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[4,4],"atlas":{"image":"atlas.png","cell":[4,4],"columns":1073741824},"states":{"Idle":{"fps":1,"frames":[0]}}}""");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// A cell taller than the atlas image, so that not even one row of cells fits, is rejected with the size of the image in
    /// the message.
    /// </summary>
    [Fact]
    public async Task LoadAsync_CellTallerThanAtlas_ThrowsInvalidData()
    {
        // Arrange
        WritePet("""{"frameSize":[8,32],"atlas":{"image":"atlas.png","cell":[8,32],"columns":4},"states":{"Idle":{"fps":1,"frames":[0]}}}""");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        (await act.Should().ThrowAsync<InvalidDataException>()).Which.Message.Should().Contain("32x16");
    }

    /// <summary>
    /// A frame that is not a cell of the atlas is rejected.
    /// </summary>
    /// <param name="cell">
    /// The number of the cell; the atlas has cells 0 to 7.
    /// </param>
    [Theory]
    [InlineData(8)]
    [InlineData(-1)]
    public async Task LoadAsync_CellOutsideAtlas_ThrowsInvalidData(int cell)
    {
        // Arrange
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":1,"frames":[%CELL%]}}}""".Replace("%CELL%", cell.ToString(System.Globalization.CultureInfo.InvariantCulture)));

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
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Walk":{"fps":1,"frames":[0]}}}""");

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
        WritePet("""{"frameSize":[8,8],%ATLAS%,"states":{"Idle":{"fps":0,"frames":[0]}}}""");

        // Act
        var act = () => _loader.LoadAsync(_directory);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }
}
