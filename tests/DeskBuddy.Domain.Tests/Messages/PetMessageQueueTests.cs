using AwesomeAssertions;
using DeskBuddy.Domain.Messages;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Messages;

/// <summary>
/// Tests for <see cref="PetMessageQueue"/>.
/// </summary>
public class PetMessageQueueTests
{
    /// <summary>
    /// How long a message stays up in the tests.
    /// </summary>
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The fake clock shared with the queue under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The queue under test.
    /// </summary>
    private readonly PetMessageQueue _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetMessageQueueTests"/> class.
    /// </summary>
    public PetMessageQueueTests()
    {
        _queue = new PetMessageQueue(Duration, _clock);
    }

    /// <summary>
    /// Creates a distinguishable message.
    /// </summary>
    /// <param name="count">
    /// A number that identifies the message.
    /// </param>
    /// <returns>
    /// A reminder message carrying the number.
    /// </returns>
    private static PetMessage Message(int count)
    {
        return new PetMessage(PetMessageKind.SedentaryReminder, [MessageAction.Dismiss], Count: count);
    }

    /// <summary>
    /// With nothing queued there is nothing to show.
    /// </summary>
    [Fact]
    public void Update_Empty_ShowsNothing()
    {
        // Act
        var changed = _queue.Update(suppressed: false);

        // Assert
        changed.Should().BeFalse();
        _queue.Current.Should().BeNull();
    }

    /// <summary>
    /// An enqueued message appears on the next update and reports a change.
    /// </summary>
    [Fact]
    public void Update_MessageWaiting_ShowsIt()
    {
        // Arrange
        _queue.Enqueue(Message(1));

        // Act
        var changed = _queue.Update(suppressed: false);

        // Assert
        changed.Should().BeTrue();
        _queue.Current.Should().NotBeNull();
        _queue.Current!.Count.Should().Be(1);
    }

    /// <summary>
    /// Messages are shown one at a time, in the order they arrived.
    /// </summary>
    [Fact]
    public void Update_TwoMessages_ShowsOldestFirstAndHoldsTheRest()
    {
        // Arrange
        _queue.Enqueue(Message(1));
        _queue.Enqueue(Message(2));

        // Act
        _queue.Update(suppressed: false);

        // Assert
        _queue.Current!.Count.Should().Be(1);
        _queue.WaitingCount.Should().Be(1);
    }

    /// <summary>
    /// The current message stays up until its duration has passed.
    /// </summary>
    [Fact]
    public void Update_BeforeDuration_KeepsCurrentMessage()
    {
        // Arrange
        _queue.Enqueue(Message(1));
        _queue.Update(suppressed: false);
        _clock.Advance(Duration - TimeSpan.FromSeconds(1));

        // Act
        var changed = _queue.Update(suppressed: false);

        // Assert
        changed.Should().BeFalse();
        _queue.Current.Should().NotBeNull();
    }

    /// <summary>
    /// The current message fades after its duration.
    /// </summary>
    [Fact]
    public void Update_AfterDuration_ExpiresMessage()
    {
        // Arrange
        _queue.Enqueue(Message(1));
        _queue.Update(suppressed: false);
        _clock.Advance(Duration);

        // Act
        var changed = _queue.Update(suppressed: false);

        // Assert
        changed.Should().BeTrue();
        _queue.Current.Should().BeNull();
    }

    /// <summary>
    /// A message with its own lifetime fades after that lifetime rather than the queue's default.
    /// </summary>
    [Fact]
    public void Update_MessageWithOwnLifetime_ExpiresEarly()
    {
        // Arrange
        var lifetime = TimeSpan.FromSeconds(8);
        _queue.Enqueue(new PetMessage(PetMessageKind.Chatter, [], Lifetime: lifetime));
        _queue.Update(suppressed: false);
        _clock.Advance(lifetime - TimeSpan.FromMilliseconds(1));
        var before = _queue.Update(suppressed: false);

        // Act
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        var after = _queue.Update(suppressed: false);

        // Assert
        before.Should().BeFalse();
        after.Should().BeTrue();
        _queue.Current.Should().BeNull();
    }

    /// <summary>
    /// A message without its own lifetime after one with a short lifetime still gets the default.
    /// </summary>
    [Fact]
    public void Update_AfterShortLivedMessage_NextUsesDefaultLifetime()
    {
        // Arrange
        _queue.Enqueue(new PetMessage(PetMessageKind.Chatter, [], Lifetime: TimeSpan.FromSeconds(8)));
        _queue.Enqueue(Message(2));
        _queue.Update(suppressed: false);
        _clock.Advance(TimeSpan.FromSeconds(8));
        _queue.Update(suppressed: false);

        // Act
        _clock.Advance(TimeSpan.FromSeconds(20));
        _queue.Update(suppressed: false);

        // Assert
        _queue.Current.Should().NotBeNull();
    }

    /// <summary>
    /// When one message expires the next one takes its place in the same update.
    /// </summary>
    [Fact]
    public void Update_ExpiryWithMessageWaiting_ShowsNextImmediately()
    {
        // Arrange
        _queue.Enqueue(Message(1));
        _queue.Enqueue(Message(2));
        _queue.Update(suppressed: false);
        _clock.Advance(Duration);

        // Act
        _queue.Update(suppressed: false);

        // Assert
        _queue.Current!.Count.Should().Be(2);
    }

    /// <summary>
    /// Dismissing closes the message and reports it.
    /// </summary>
    [Fact]
    public void Dismiss_WithCurrentMessage_ClosesIt()
    {
        // Arrange
        _queue.Enqueue(Message(1));
        _queue.Update(suppressed: false);

        // Act
        var closed = _queue.Dismiss();

        // Assert
        closed.Should().BeTrue();
        _queue.Current.Should().BeNull();
    }

    /// <summary>
    /// Dismissing with nothing on screen does nothing.
    /// </summary>
    [Fact]
    public void Dismiss_NothingShown_ReturnsFalse()
    {
        // Act
        var closed = _queue.Dismiss();

        // Assert
        closed.Should().BeFalse();
    }

    /// <summary>
    /// While suppressed, new messages are held back rather than shown.
    /// </summary>
    [Fact]
    public void Update_Suppressed_HoldsMessagesBack()
    {
        // Arrange
        _queue.Enqueue(Message(1));

        // Act
        var changed = _queue.Update(suppressed: true);

        // Assert
        changed.Should().BeFalse();
        _queue.Current.Should().BeNull();
        _queue.WaitingCount.Should().Be(1);
    }

    /// <summary>
    /// A message already on screen when suppression starts is pulled back to the front of the line, not lost.
    /// </summary>
    [Fact]
    public void Update_SuppressionStartsWhileShowing_RequeuesAtFront()
    {
        // Arrange
        _queue.Enqueue(Message(1));
        _queue.Enqueue(Message(2));
        _queue.Update(suppressed: false);

        // Act
        var changed = _queue.Update(suppressed: true);

        // Assert
        changed.Should().BeTrue();
        _queue.Current.Should().BeNull();
        _queue.WaitingCount.Should().Be(2);
    }

    /// <summary>
    /// Once suppression ends the held-back message comes back first, with a fresh display time.
    /// </summary>
    [Fact]
    public void Update_SuppressionEnds_ShowsHeldMessageFirstWithFreshTimer()
    {
        // Arrange
        _queue.Enqueue(Message(1));
        _queue.Enqueue(Message(2));
        _queue.Update(suppressed: false);
        _queue.Update(suppressed: true);
        _clock.Advance(TimeSpan.FromMinutes(10));

        // Act
        _queue.Update(suppressed: false);
        _clock.Advance(Duration - TimeSpan.FromSeconds(1));
        var changedBeforeExpiry = _queue.Update(suppressed: false);

        // Assert
        _queue.Current!.Count.Should().Be(1);
        changedBeforeExpiry.Should().BeFalse();
    }
}
