using AwesomeAssertions;

namespace DeskBuddy.Domain.Tests;

/// <summary>
/// Tests for <see cref="PetStateExtensions"/>.
/// </summary>
public class PetStateExtensionsTests
{
    /// <summary>
    /// Falling, with or without the umbrella open, counts as falling; nothing else does.
    /// </summary>
    /// <param name="state">
    /// The state.
    /// </param>
    /// <param name="expected">
    /// Whether it counts as falling.
    /// </param>
    [Theory]
    [InlineData(PetState.Fall, true)]
    [InlineData(PetState.Glide, true)]
    [InlineData(PetState.Drag, false)]
    [InlineData(PetState.Climb, false)]
    [InlineData(PetState.Idle, false)]
    public void IsFalling_State_MatchesExpectation(PetState state, bool expected)
    {
        // Act
        var result = state.IsFalling();

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Only climbing a side and hanging from the top count as crawling.
    /// </summary>
    /// <param name="state">
    /// The state.
    /// </param>
    /// <param name="expected">
    /// Whether it counts as crawling.
    /// </param>
    [Theory]
    [InlineData(PetState.Climb, true)]
    [InlineData(PetState.ClimbDown, true)]
    [InlineData(PetState.Ceiling, true)]
    [InlineData(PetState.Idle, false)]
    [InlineData(PetState.Walk, false)]
    [InlineData(PetState.Hang, false)]
    [InlineData(PetState.Fall, false)]
    [InlineData(PetState.Drag, false)]
    public void IsCrawl_State_MatchesExpectation(PetState state, bool expected)
    {
        // Act
        var result = state.IsCrawl();

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// A crawl that gives way to a plain state, an alert or sleep lets go and falls.
    /// </summary>
    /// <param name="previous">
    /// The crawling state.
    /// </param>
    /// <param name="next">
    /// The state it changes to.
    /// </param>
    [Theory]
    [InlineData(PetState.Climb, PetState.Idle)]
    [InlineData(PetState.Ceiling, PetState.Idle)]
    [InlineData(PetState.Ceiling, PetState.Alert)]
    [InlineData(PetState.Climb, PetState.Sleep)]
    [InlineData(PetState.ClimbDown, PetState.Idle)]
    public void EndsCrawlInFall_CrawlGivesWayToOtherState_ReturnsTrue(PetState previous, PetState next)
    {
        // Act
        var result = previous.EndsCrawlInFall(next);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Going on crawling, being picked up, and a fall already begun are no reason to start a fall.
    /// </summary>
    /// <param name="previous">
    /// The state the pet was in.
    /// </param>
    /// <param name="next">
    /// The state it changes to.
    /// </param>
    [Theory]
    [InlineData(PetState.Climb, PetState.Ceiling)]
    [InlineData(PetState.Ceiling, PetState.ClimbDown)]
    [InlineData(PetState.Ceiling, PetState.Climb)]
    [InlineData(PetState.Climb, PetState.Drag)]
    [InlineData(PetState.Ceiling, PetState.Fall)]
    [InlineData(PetState.Ceiling, PetState.Glide)]
    [InlineData(PetState.Idle, PetState.Alert)]
    [InlineData(PetState.Walk, PetState.Idle)]
    public void EndsCrawlInFall_OtherChange_ReturnsFalse(PetState previous, PetState next)
    {
        // Act
        var result = previous.EndsCrawlInFall(next);

        // Assert
        result.Should().BeFalse();
    }
}
