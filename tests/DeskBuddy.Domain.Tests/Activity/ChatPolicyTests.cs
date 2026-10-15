using AwesomeAssertions;
using DeskBuddy.Domain.Activity;

namespace DeskBuddy.Domain.Tests.Activity;

/// <summary>
/// Tests for <see cref="ChatPolicy"/>.
/// </summary>
public class ChatPolicyTests
{
    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly ChatPolicy _policy = new() { Processes = ["Weixin", "WhatsApp"] };

    /// <summary>
    /// A chat application in front is chatting, whatever the case of its name.
    /// </summary>
    /// <param name="name">
    /// The foreground process name.
    /// </param>
    [Theory]
    [InlineData("Weixin")]
    [InlineData("WEIXIN")]
    [InlineData("whatsapp")]
    public void IsChatting_ChatApplication_IsTrue(string name)
    {
        // Act
        var result = _policy.IsChatting(name);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Any other application, or none, is not chatting.
    /// </summary>
    /// <param name="name">
    /// The foreground process name.
    /// </param>
    [Theory]
    [InlineData("Code")]
    [InlineData("")]
    [InlineData(null)]
    public void IsChatting_OtherApplication_IsFalse(string? name)
    {
        // Act
        var result = _policy.IsChatting(name);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Without a list nothing counts as chatting.
    /// </summary>
    [Fact]
    public void IsChatting_EmptyList_IsFalse()
    {
        // Act
        var result = new ChatPolicy().IsChatting("Weixin");

        // Assert
        result.Should().BeFalse();
    }
}
