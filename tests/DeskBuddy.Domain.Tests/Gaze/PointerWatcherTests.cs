using AwesomeAssertions;
using DeskBuddy.Domain.Gaze;

namespace DeskBuddy.Domain.Tests.Gaze;

/// <summary>
/// Tests for <see cref="PointerWatcher"/>.
/// </summary>
public class PointerWatcherTests
{
    /// <summary>
    /// A pet 64 pixels wide with its centre at 500, 500.
    /// </summary>
    private static readonly Func<PetBox> Box = () => new PetBox(500, 500, 64);

    /// <summary>
    /// The object under test.
    /// </summary>
    private readonly PointerWatcher _watcher = new(new MouseShakeDetector());

    /// <summary>
    /// A shake makes the pet dizzy.
    /// </summary>
    [Fact]
    public void Read_Shake_BecomesDizzy()
    {
        // Act
        var reactions = Shake(startAtMilliseconds: 0);

        // Assert
        reactions.Should().Contain(reaction => reaction.BecomeDizzy);
    }

    /// <summary>
    /// A second shake within the cooldown does not make the pet dizzy again.
    /// </summary>
    [Fact]
    public void Read_ShakeAgainWithinCooldown_IsIgnored()
    {
        // Arrange
        Shake(startAtMilliseconds: 0);

        // Act
        var reactions = Shake(startAtMilliseconds: 2000);

        // Assert
        reactions.Should().NotContain(reaction => reaction.BecomeDizzy);
    }

    /// <summary>
    /// After the cooldown a shake counts again.
    /// </summary>
    [Fact]
    public void Read_ShakeAfterCooldown_BecomesDizzyAgain()
    {
        // Arrange
        Shake(startAtMilliseconds: 0);

        // Act
        var reactions = Shake(startAtMilliseconds: 10000);

        // Assert
        reactions.Should().Contain(reaction => reaction.BecomeDizzy);
    }

    /// <summary>
    /// A shake does nothing when dizziness is not allowed.
    /// </summary>
    [Fact]
    public void Read_ShakeNotAllowed_DoesNotBecomeDizzy()
    {
        // Act
        var reactions = Shake(startAtMilliseconds: 0, dizzyAllowed: false);

        // Assert
        reactions.Should().NotContain(reaction => reaction.BecomeDizzy);
    }

    /// <summary>
    /// The eyes turn only on every fourth reading, towards the pointer.
    /// </summary>
    [Fact]
    public void Read_PointerToTheRight_LooksRightOnEveryFourthReading()
    {
        // Act
        var reactions = Enumerable.Range(0, 8)
            .Select(index => _watcher.Read(TimeSpan.FromMilliseconds(50 * index), 900, 500, true, true, Box))
            .ToList();

        // Assert
        reactions.Select(reaction => reaction.Gaze).Should().Equal(
            null, null, null, GazeDirection.Right, null, null, null, GazeDirection.Right);
    }

    /// <summary>
    /// A pet that cannot look is told to look ahead.
    /// </summary>
    [Fact]
    public void Read_CannotLook_LooksAhead()
    {
        // Act
        var reactions = Enumerable.Range(0, 4)
            .Select(index => _watcher.Read(TimeSpan.FromMilliseconds(50 * index), 900, 500, true, false, Box))
            .ToList();

        // Assert
        reactions[3].Gaze.Should().Be(GazeDirection.None);
    }

    /// <summary>
    /// Shaking the mouse up and down works as well.
    /// </summary>
    [Fact]
    public void Read_VerticalShake_BecomesDizzy()
    {
        // Arrange
        var detector = new MouseShakeDetector();
        var detected = false;

        // Act
        foreach (var (position, index) in new[] { 0, 150, 0, 150, 0, 150, 0 }.Select((value, index) => (value, index)))
        {
            detected |= detector.Update(TimeSpan.FromMilliseconds(100 * index), 300, position);
        }

        // Assert
        detected.Should().BeTrue();
    }

    /// <summary>
    /// Feeds a quick side-to-side shake.
    /// </summary>
    /// <param name="startAtMilliseconds">
    /// When the shake begins.
    /// </param>
    /// <param name="dizzyAllowed">
    /// Whether dizziness is allowed.
    /// </param>
    /// <returns>
    /// The reactions to each reading.
    /// </returns>
    private List<PointerReaction> Shake(int startAtMilliseconds, bool dizzyAllowed = true)
    {
        var positions = new[] { 0, 150, 0, 150, 0, 150, 0 };
        return
        [
            .. positions.Select((x, index) => _watcher.Read(
                TimeSpan.FromMilliseconds(startAtMilliseconds + (100 * index)),
                x,
                0,
                dizzyAllowed,
                false,
                Box)),
        ];
    }
}
