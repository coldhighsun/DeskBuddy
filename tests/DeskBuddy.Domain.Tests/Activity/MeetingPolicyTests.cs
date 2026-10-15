namespace DeskBuddy.Domain.Tests.Activity;

/// <summary>
/// Tests for <see cref="MeetingPolicy"/>.
/// </summary>
public class MeetingPolicyTests
{
    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly MeetingPolicy _policy = new() { Processes = ["Zoom", "wemeetapp"] };

    /// <summary>
    /// A meeting application in front is a meeting, whatever the case of its name.
    /// </summary>
    /// <param name="name">
    /// The foreground process name.
    /// </param>
    [Theory]
    [InlineData("Zoom")]
    [InlineData("ZOOM")]
    [InlineData("wemeetapp")]
    public void IsInMeeting_MeetingApplication_IsTrue(string name)
    {
        // Act
        var result = _policy.IsInMeeting(name);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Any other application, or none, is not a meeting.
    /// </summary>
    /// <param name="name">
    /// The foreground process name.
    /// </param>
    [Theory]
    [InlineData("Code")]
    [InlineData("")]
    [InlineData(null)]
    public void IsInMeeting_OtherApplication_IsFalse(string? name)
    {
        // Act
        var result = _policy.IsInMeeting(name);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Without a list nothing is a meeting.
    /// </summary>
    [Fact]
    public void IsInMeeting_NoProcesses_IsFalse()
    {
        // Act
        var result = new MeetingPolicy().IsInMeeting("Zoom");

        // Assert
        result.Should().BeFalse();
    }
}
