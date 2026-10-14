using AwesomeAssertions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Infrastructure.Pets;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// Finds the shipped pets folder by walking up from the test output directory.
    /// </summary>
    /// <returns>
    /// The path of the <c>pets</c> folder.
    /// </returns>
    private static string FindPetsDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "pets");
            if (Directory.Exists(Path.Combine(candidate, "robot")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The repository's pets folder was not found above the test output.");
    }

    /// <summary>
    /// The robot pet loads, with every frame file present, and has its own animation for every state but falling, which
    /// borrows the dragged one, including holding the umbrella and sitting at the computer. It rolls on a tread and does
    /// not hop, so it has none for that either.
    /// </summary>
    [Fact]
    public async Task Robot_Loads_WithAnimationForEveryState()
    {
        // Act
        var pet = await _loader.LoadAsync(Path.Combine(FindPetsDirectory(), "robot"), TestContext.Current.CancellationToken);

        // Assert
        pet.Name.Should().Be("robot");
        pet.Animations.Keys.Should().BeEquivalentTo(Enum.GetValues<PetState>().Where(state => state is not (PetState.Fall or PetState.Hop)));
        pet.Animations[PetState.Coding].FramePaths.Should().HaveCountGreaterThan(1);
        pet.GazeAnimations.Keys.Should().BeEquivalentTo(
            [GazeDirection.Left, GazeDirection.Right, GazeDirection.Up, GazeDirection.Down]);
    }

    /// <summary>
    /// The cat pet loads with every frame file present and covers the everyday and screen-edge states, leaving the
    /// developer-specific ones to the fallback rules.
    /// </summary>
    [Fact]
    public async Task Cat_Loads_WithEverydayAndScreenEdgeStates()
    {
        // Act
        var pet = await _loader.LoadAsync(Path.Combine(FindPetsDirectory(), "cat"), TestContext.Current.CancellationToken);

        // Assert
        pet.Name.Should().Be("cat");
        pet.Animations.Keys.Should().Contain(
            [PetState.Idle, PetState.Walk, PetState.Sleep, PetState.Drag, PetState.Happy, PetState.Glide, PetState.Hang, PetState.Climb, PetState.ClimbDown, PetState.Ceiling, PetState.Hop]);
        pet.Animations.Keys.Should().NotContain(PetState.Coding);
        pet.Animations[PetState.Idle].FramePaths.Should().HaveCountGreaterThan(1);
        pet.GazeAnimations.Keys.Should().BeEquivalentTo(
            [GazeDirection.Left, GazeDirection.Right, GazeDirection.Up, GazeDirection.Down]);
    }
}
