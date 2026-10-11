using AwesomeAssertions;
using DeskBuddy.Domain.Skins;

namespace DeskBuddy.Domain.Tests.Skins;

/// <summary>
/// Tests for <see cref="Skin"/>.
/// </summary>
public class SkinTests
{
    /// <summary>
    /// A skin without Idle must be rejected because Idle is the fallback.
    /// </summary>
    [Fact]
    public void Constructor_NoIdleAnimation_Throws()
    {
        // Arrange
        var animations = new Dictionary<PetState, SkinAnimation> { [PetState.Walk] = Animation("walk.png") };

        // Act
        var act = () => new Skin("test", 128, 128, animations);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// A defined state returns its own animation.
    /// </summary>
    [Fact]
    public void GetAnimation_DefinedState_ReturnsThatAnimation()
    {
        // Arrange
        var walk = Animation("walk.png");
        var skin = new Skin("test", 128, 128, new Dictionary<PetState, SkinAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Walk] = walk,
        });

        // Act
        var result = skin.GetAnimation(PetState.Walk);

        // Assert
        result.Should().BeSameAs(walk);
    }

    /// <summary>
    /// A state the skin does not define falls back to Idle.
    /// </summary>
    [Fact]
    public void GetAnimation_MissingState_FallsBackToIdle()
    {
        // Arrange
        var idle = Animation("idle.png");
        var skin = new Skin("test", 128, 128, new Dictionary<PetState, SkinAnimation> { [PetState.Idle] = idle });

        // Act
        var result = skin.GetAnimation(PetState.Sleep);

        // Assert
        result.Should().BeSameAs(idle);
    }

    /// <summary>
    /// Builds a single-frame animation for tests.
    /// </summary>
    /// <param name="name">
    /// Frame file name.
    /// </param>
    /// <returns>
    /// An animation at 4 fps with one frame.
    /// </returns>
    private static SkinAnimation Animation(string name)
    {
        return new SkinAnimation(4, [name]);
    }
}