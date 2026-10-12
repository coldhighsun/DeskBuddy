using AwesomeAssertions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Infrastructure.Skins;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskBuddy.Infrastructure.Tests.Skins;

/// <summary>
/// Tests for the skins that ship in the repository's <c>skins</c> folder.
/// </summary>
public class ShippedSkinsTests
{
    /// <summary>
    /// The loader used to read the shipped skins.
    /// </summary>
    private readonly FolderSkinLoader _loader = new(NullLogger<FolderSkinLoader>.Instance);

    /// <summary>
    /// Finds the shipped skins folder by walking up from the test output directory.
    /// </summary>
    /// <returns>
    /// The path of the <c>skins</c> folder.
    /// </returns>
    private static string FindSkinsDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "skins");
            if (Directory.Exists(Path.Combine(candidate, "robot")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The repository's skins folder was not found above the test output.");
    }

    /// <summary>
    /// The robot skin loads, with every frame file present, and has its own animation for every state but falling, which
    /// borrows the dragged one, including holding the umbrella and sitting at the computer.
    /// </summary>
    [Fact]
    public async Task Robot_Loads_WithAnimationForEveryState()
    {
        // Act
        var skin = await _loader.LoadAsync(Path.Combine(FindSkinsDirectory(), "robot"));

        // Assert
        skin.Name.Should().Be("robot");
        skin.Animations.Keys.Should().BeEquivalentTo(Enum.GetValues<PetState>().Where(state => state != PetState.Fall));
        skin.Animations[PetState.Coding].FramePaths.Should().HaveCountGreaterThan(1);
        skin.GazeAnimations.Keys.Should().BeEquivalentTo(
            [GazeDirection.Left, GazeDirection.Right, GazeDirection.Up, GazeDirection.Down]);
    }
}
