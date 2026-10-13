using AwesomeAssertions;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.Domain.Tests.Pets;

/// <summary>
/// Tests for <see cref="Pet"/>.
/// </summary>
public class PetTests
{
    /// <summary>
    /// A pet without Idle must be rejected because Idle is the fallback.
    /// </summary>
    [Fact]
    public void Constructor_NoIdleAnimation_Throws()
    {
        // Arrange
        var animations = new Dictionary<PetState, PetAnimation> { [PetState.Walk] = Animation("walk.png") };

        // Act
        var act = () => new Pet("test", 128, 128, animations);

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
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Walk] = walk,
        });

        // Act
        var result = pet.GetAnimation(PetState.Walk);

        // Assert
        result.Should().BeSameAs(walk);
    }

    /// <summary>
    /// A state the pet does not define falls back to Idle.
    /// </summary>
    [Fact]
    public void GetAnimation_MissingState_FallsBackToIdle()
    {
        // Arrange
        var idle = Animation("idle.png");
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = idle });

        // Act
        var result = pet.GetAnimation(PetState.Sleep);

        // Assert
        result.Should().BeSameAs(idle);
    }

    /// <summary>
    /// A state the pet lacks shows its closest fallback if the pet has that.
    /// </summary>
    [Fact]
    public void GetAnimation_MissingStateWithFallback_UsesTheFallback()
    {
        // Arrange
        var coding = Animation("coding.png");
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Coding] = coding,
        });

        // Act
        var result = pet.GetAnimation(PetState.NightCoding);

        // Assert
        result.Should().BeSameAs(coding);
    }

    /// <summary>
    /// A state whose fallback is missing too ends up at Idle.
    /// </summary>
    [Fact]
    public void GetAnimation_FallbackAlsoMissing_FallsBackToIdle()
    {
        // Arrange
        var idle = Animation("idle.png");
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = idle });

        // Act
        var result = pet.GetAnimation(PetState.NightCoding);

        // Assert
        result.Should().BeSameAs(idle);
    }

    /// <summary>
    /// Following the fallbacks of any state always ends, and ends without a loop.
    /// </summary>
    [Fact]
    public void PetStateFallback_ForEveryState_ChainEnds()
    {
        foreach (var state in Enum.GetValues<PetState>())
        {
            var steps = 0;
            for (PetState? current = state; current is not null; current = PetStateFallback.Of(current.Value))
            {
                steps++;
                steps.Should().BeLessThan(10, $"the fallbacks of {state} should not loop");
            }
        }
    }

    /// <summary>
    /// An idle pet looks in the direction asked for if the pet has that look.
    /// </summary>
    [Fact]
    public void GetAnimation_IdleWithGazeVariant_ReturnsTheVariant()
    {
        // Arrange
        var left = Animation("left.png");
        var pet = new Pet(
            "test",
            128,
            128,
            new Dictionary<PetState, PetAnimation> { [PetState.Idle] = Animation("idle.png") },
            new Dictionary<GazeDirection, PetAnimation> { [GazeDirection.Left] = left });

        // Act
        var result = pet.GetAnimation(PetState.Idle, GazeDirection.Left);

        // Assert
        result.Should().BeSameAs(left);
        pet.CanLookAround.Should().BeTrue();
    }

    /// <summary>
    /// A look the pet lacks, or any look in another state, shows the plain animation.
    /// </summary>
    [Fact]
    public void GetAnimation_GazeVariantMissingOrNotIdle_ReturnsThePlainAnimation()
    {
        // Arrange
        var idle = Animation("idle.png");
        var walk = Animation("walk.png");
        var pet = new Pet(
            "test",
            128,
            128,
            new Dictionary<PetState, PetAnimation> { [PetState.Idle] = idle, [PetState.Walk] = walk },
            new Dictionary<GazeDirection, PetAnimation> { [GazeDirection.Left] = Animation("left.png") });

        // Act
        var up = pet.GetAnimation(PetState.Idle, GazeDirection.Up);
        var walking = pet.GetAnimation(PetState.Walk, GazeDirection.Left);

        // Assert
        up.Should().BeSameAs(idle);
        walking.Should().BeSameAs(walk);
    }

    /// <summary>
    /// A pet without looks cannot look around.
    /// </summary>
    [Fact]
    public void CanLookAround_NoGazeVariants_IsFalse()
    {
        // Arrange
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation> { [PetState.Idle] = Animation("idle.png") });

        // Assert
        pet.CanLookAround.Should().BeFalse();
    }

    /// <summary>
    /// A dizzy pet without a dizzy animation looks tired.
    /// </summary>
    [Fact]
    public void GetAnimation_DizzyMissing_UsesTired()
    {
        // Arrange
        var tired = Animation("tired.png");
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Tired] = tired,
        });

        // Act
        var result = pet.GetAnimation(PetState.Dizzy);

        // Assert
        result.Should().BeSameAs(tired);
    }

    /// <summary>
    /// A falling pet without a falling animation looks dragged.
    /// </summary>
    [Fact]
    public void GetAnimation_FallMissing_UsesDrag()
    {
        // Arrange
        var drag = Animation("drag.png");
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Drag] = drag,
        });

        // Act
        var result = pet.GetAnimation(PetState.Fall);

        // Assert
        result.Should().BeSameAs(drag);
    }

    /// <summary>
    /// A pet climbing or hanging from the top with a pet that lacks those pictures looks like it walks.
    /// </summary>
    /// <param name="state">
    /// The state the pet lacks.
    /// </param>
    [Theory]
    [InlineData(PetState.Climb)]
    [InlineData(PetState.Ceiling)]
    public void GetAnimation_CrawlStateMissing_UsesWalk(PetState state)
    {
        // Arrange
        var walk = Animation("walk.png");
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Walk] = walk,
        });

        // Act
        var result = pet.GetAnimation(state);

        // Assert
        result.Should().BeSameAs(walk);
    }

    /// <summary>
    /// A pet climbing down with a pet that has only the climbing up pictures shows those.
    /// </summary>
    [Fact]
    public void GetAnimation_ClimbDownMissing_UsesClimb()
    {
        // Arrange
        var climb = Animation("climb.png");
        var pet = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Walk] = Animation("walk.png"),
            [PetState.Climb] = climb,
        });

        // Act
        var result = pet.GetAnimation(PetState.ClimbDown);

        // Assert
        result.Should().BeSameAs(climb);
    }

    /// <summary>
    /// A pet holding an open umbrella with a pet that has no picture for it shows the falling one, or the dragged one if
    /// that is missing too.
    /// </summary>
    [Fact]
    public void GetAnimation_GlideMissing_UsesFallThenDrag()
    {
        // Arrange
        var fall = Animation("fall.png");
        var drag = Animation("drag.png");
        var withFall = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Drag] = drag,
            [PetState.Fall] = fall,
        });
        var withoutFall = new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = Animation("idle.png"),
            [PetState.Drag] = drag,
        });

        // Act
        var first = withFall.GetAnimation(PetState.Glide);
        var second = withoutFall.GetAnimation(PetState.Glide);

        // Assert
        first.Should().BeSameAs(fall);
        second.Should().BeSameAs(drag);
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
    private static PetAnimation Animation(string name)
    {
        return new PetAnimation(4, [name]);
    }
}
