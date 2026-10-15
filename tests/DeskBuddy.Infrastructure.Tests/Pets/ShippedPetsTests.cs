using System.Globalization;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Infrastructure.Pets;

namespace DeskBuddy.Infrastructure.Tests.Pets;

/// <summary>
/// Tests for the pets that ship in the repository's <c>pets</c> folder.
/// </summary>
public class ShippedPetsTests
{
    /// <summary>
    /// The loader used to read the shipped pets.
    /// </summary>
    private readonly FolderPetLoader _loader = new(NullLogger<FolderPetLoader>.Instance);

    /// <summary>
    /// The robot pet loads, with every frame file present, and has its own animation for every state but falling, which
    /// borrows the dragged one, including holding the umbrella and sitting at the computer. It rolls on a tread and does
    /// not hop, so it has none for that either.
    /// </summary>
    [Fact]
    public async Task Robot_Loads_WithAnimationForEveryState()
    {
        // Act
        var pet = await _loader.LoadAsync(Path.Combine(RepositoryPaths.Pets, "robot"), TestContext.Current.CancellationToken);

        // Assert
        pet.Name.Should().Be("robot");
        pet.Animations.Keys.Should().BeEquivalentTo(Enum.GetValues<PetState>().Where(state => state is not (PetState.Fall or PetState.Hop)));
        pet.Animations[PetState.Coding].FrameIds.Should().HaveCountGreaterThan(1);
        pet.GazeAnimations.Keys.Should().BeEquivalentTo(
            [GazeDirection.Left, GazeDirection.Right, GazeDirection.Up, GazeDirection.Down]);
    }

    /// <summary>
    /// The cat pet loads with every frame file present and covers the everyday, screen-edge and work
    /// states (coding, reading, meetings and so on), leaving the rest to the fallback rules.
    /// </summary>
    [Fact]
    public async Task Cat_Loads_WithEverydayAndScreenEdgeStates()
    {
        // Act
        var pet = await _loader.LoadAsync(Path.Combine(RepositoryPaths.Pets, "cat"), TestContext.Current.CancellationToken);

        // Assert
        pet.Name.Should().Be("cat");
        pet.Animations.Keys.Should().Contain(
            [PetState.Idle, PetState.Walk, PetState.Sleep, PetState.Drag, PetState.Happy, PetState.Glide, PetState.Hang, PetState.Climb, PetState.ClimbDown, PetState.Ceiling, PetState.Hop]);
        pet.Animations.Keys.Should().Contain(
            [PetState.Coding, PetState.NightCoding, PetState.Building, PetState.Focus, PetState.Reading, PetState.Meeting, PetState.Chatting]);
        pet.Animations.Keys.Should().NotContain(PetState.Charging);
        pet.Animations[PetState.Idle].FrameIds.Should().HaveCountGreaterThan(1);
        pet.GazeAnimations.Keys.Should().BeEquivalentTo(
            [GazeDirection.Left, GazeDirection.Right, GazeDirection.Up, GazeDirection.Down]);
    }

    /// <summary>
    /// A shipped pet is only its manifest, the atlas that holds all its frames and the icon for the tray; the frames are
    /// all in the atlas, and they are not single files next to it.
    /// </summary>
    /// <param name="name">
    /// The pet's folder name.
    /// </param>
    [Theory]
    [InlineData("robot")]
    [InlineData("cat")]
    [InlineData("dog")]
    [InlineData("rabbit")]
    [InlineData("penguin")]
    [InlineData("ghost")]
    public async Task ShippedPet_Folder_HoldsOnlyManifestAtlasAndIcon(string name)
    {
        // Arrange
        var directory = Path.Combine(RepositoryPaths.Pets, name);

        // Act
        var pet = await _loader.LoadAsync(directory, TestContext.Current.CancellationToken);

        // Assert
        Directory.GetFiles(directory).Select(Path.GetFileName).Should().BeEquivalentTo(["manifest.json", "atlas.png", "icon.png"]);
        var atlas = Path.Combine(directory, "atlas.png");
        pet.Animations.Values.Concat(pet.GazeAnimations.Values)
            .SelectMany(animation => animation.FrameIds)
            .Should().OnlyContain(frame => string.Equals(FrameId.Parse(frame).Path, atlas, StringComparison.OrdinalIgnoreCase) && FrameId.Parse(frame).Area != null);
    }

    /// <summary>
    /// Every shipped pet names itself in English and in Chinese, so the panel shows its name in the language in use.
    /// </summary>
    /// <param name="name">
    /// The pet's folder name.
    /// </param>
    [Theory]
    [InlineData("robot")]
    [InlineData("cat")]
    [InlineData("dog")]
    [InlineData("rabbit")]
    [InlineData("penguin")]
    [InlineData("ghost")]
    public async Task ShippedPet_Loads_WithEnglishAndChineseNames(string name)
    {
        // Act
        var pet = await _loader.LoadAsync(Path.Combine(RepositoryPaths.Pets, name), TestContext.Current.CancellationToken);

        // Assert
        var english = pet.GetDisplayName(CultureInfo.GetCultureInfo("en-US"));
        var chinese = pet.GetDisplayName(CultureInfo.GetCultureInfo("zh-CN"));
        english.Should().NotBe(chinese);
        english.Should().MatchRegex("^[A-Za-z ]+$");
        chinese.Should().MatchRegex("\\p{IsCJKUnifiedIdeographs}");
    }

    /// <summary>
    /// The pets drawn from the shared template load with every frame file present and cover the everyday, screen-edge and work
    /// states; only the ones that define a hop animation can hop.
    /// </summary>
    /// <param name="name">The pet's folder name.</param>
    /// <param name="canHop">Whether the pet defines the optional hop animation.</param>
    [Theory]
    [InlineData("dog", true)]
    [InlineData("rabbit", false)]
    [InlineData("penguin", false)]
    [InlineData("ghost", false)]
    public async Task TemplatePet_Loads_WithEverydayAndScreenEdgeStates(string name, bool canHop)
    {
        // Act
        var pet = await _loader.LoadAsync(Path.Combine(RepositoryPaths.Pets, name), TestContext.Current.CancellationToken);

        // Assert
        pet.Name.Should().Be(name);
        pet.Animations.Keys.Should().Contain(
            [PetState.Idle, PetState.Walk, PetState.Sleep, PetState.Drag, PetState.Happy, PetState.Glide, PetState.Hang, PetState.Climb, PetState.ClimbDown, PetState.Ceiling]);
        pet.Animations.ContainsKey(PetState.Hop).Should().Be(canHop);
        pet.Animations.Keys.Should().Contain(
            [PetState.Coding, PetState.NightCoding, PetState.Building, PetState.Focus, PetState.Reading, PetState.Meeting, PetState.Chatting]);
        pet.Animations.Keys.Should().NotContain(PetState.Charging);
        pet.Animations[PetState.Idle].FrameIds.Should().HaveCountGreaterThan(1);
        pet.GazeAnimations.Keys.Should().BeEquivalentTo(
            [GazeDirection.Left, GazeDirection.Right, GazeDirection.Up, GazeDirection.Down]);
    }
}
