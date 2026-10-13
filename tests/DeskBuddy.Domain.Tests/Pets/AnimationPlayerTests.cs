using AwesomeAssertions;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.Domain.Tests.Pets;

/// <summary>
/// Tests for <see cref="AnimationPlayer"/>.
/// </summary>
public class AnimationPlayerTests
{
    /// <summary>
    /// Reaching the frame duration moves to the next frame.
    /// </summary>
    [Fact]
    public void Advance_FrameDurationElapsed_MovesToNextFrame()
    {
        // Arrange
        var player = new AnimationPlayer(CreatePet());

        // Act
        var changed = player.Advance(TimeSpan.FromMilliseconds(500));

        // Assert
        changed.Should().BeTrue();
        player.FrameIndex.Should().Be(1);
        player.CurrentFramePath.Should().Be("idle_1.png");
    }

    /// <summary>
    /// Less than one frame duration must not change the frame.
    /// </summary>
    [Fact]
    public void Advance_LessThanFrameDuration_DoesNotChangeFrame()
    {
        // Arrange
        var player = new AnimationPlayer(CreatePet());

        // Act
        var changed = player.Advance(TimeSpan.FromMilliseconds(400));

        // Assert
        changed.Should().BeFalse();
        player.FrameIndex.Should().Be(0);
    }

    /// <summary>
    /// Zero and negative deltas are ignored.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Advance_NonPositiveDelta_DoesNothing(int milliseconds)
    {
        // Arrange
        var player = new AnimationPlayer(CreatePet());

        // Act
        var changed = player.Advance(TimeSpan.FromMilliseconds(milliseconds));

        // Assert
        changed.Should().BeFalse();
        player.FrameIndex.Should().Be(0);
    }

    /// <summary>
    /// Leftover time below one frame carries over to the next call instead of being dropped.
    /// </summary>
    [Fact]
    public void Advance_PartialTimes_Accumulate()
    {
        // Arrange
        var player = new AnimationPlayer(CreatePet());

        // Act
        player.Advance(TimeSpan.FromMilliseconds(300));
        var changed = player.Advance(TimeSpan.FromMilliseconds(300));

        // Assert
        changed.Should().BeTrue();
        player.FrameIndex.Should().Be(1);
    }

    /// <summary>
    /// Playback loops back to the first frame after the last one.
    /// </summary>
    [Fact]
    public void Advance_PastLastFrame_LoopsToStart()
    {
        // Arrange
        var player = new AnimationPlayer(CreatePet());

        // Act
        player.Advance(TimeSpan.FromMilliseconds(500));
        player.Advance(TimeSpan.FromMilliseconds(500));

        // Assert
        player.FrameIndex.Should().Be(0);
    }

    /// <summary>
    /// A single-frame animation never reports a frame change.
    /// </summary>
    [Fact]
    public void Advance_SingleFrameAnimation_NeverChanges()
    {
        // Arrange
        var player = new AnimationPlayer(CreatePet());
        player.SetState(PetState.Sleep);

        // Act
        var changed = player.Advance(TimeSpan.FromSeconds(10));

        // Assert
        changed.Should().BeFalse();
        player.FrameIndex.Should().Be(0);
    }

    /// <summary>
    /// Switching to a state with a different animation restarts playback and reports a change.
    /// </summary>
    [Fact]
    public void SetState_DifferentAnimation_ResetsFrameAndReportsChange()
    {
        // Arrange
        var player = new AnimationPlayer(CreatePet());
        player.Advance(TimeSpan.FromMilliseconds(500));

        // Act
        var changed = player.SetState(PetState.Sleep);

        // Assert
        changed.Should().BeTrue();
        player.FrameIndex.Should().Be(0);
        player.CurrentFramePath.Should().Be("sleep_0.png");
    }

    /// <summary>
    /// A state that resolves to the animation already playing must not restart it.
    /// </summary>
    [Fact]
    public void SetState_MissingStateResolvingToCurrentAnimation_DoesNotRestart()
    {
        // Arrange
        var player = new AnimationPlayer(CreatePet());
        player.Advance(TimeSpan.FromMilliseconds(500));

        // Act
        var changed = player.SetState(PetState.Walk);

        // Assert
        changed.Should().BeFalse();
        player.FrameIndex.Should().Be(1);
        player.State.Should().Be(PetState.Walk);
    }

    /// <summary>
    /// Creates a pet with a 2-frame Idle at 2 fps (500 ms per frame) and a 1-frame Sleep.
    /// </summary>
    /// <returns>
    /// The test pet.
    /// </returns>
    private static Pet CreatePet()
    {
        return new Pet("test", 128, 128, new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = new(2, ["idle_0.png", "idle_1.png"]),
            [PetState.Sleep] = new(1, ["sleep_0.png"]),
        });
    }
}