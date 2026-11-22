namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="InstanceSignal"/>.
/// </summary>
public sealed class InstanceSignalTests
{
    /// <summary>
    /// How long a test waits for a signal to arrive.
    /// </summary>
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A signal reaches the instance listening on the same name.
    /// </summary>
    [Fact]
    public void TrySignal_Listening_RunsCallback()
    {
        // Arrange
        var name = UniqueName();
        using var received = new ManualResetEventSlim();
        using var signal = new InstanceSignal(name, received.Set);

        // Act
        var sent = InstanceSignal.TrySignal(name);

        // Assert
        sent.Should().BeTrue();
        received.Wait(SignalTimeout, TestContext.Current.CancellationToken).Should().BeTrue();
    }

    /// <summary>
    /// Each signal runs the callback again, not just the first one.
    /// </summary>
    [Fact]
    public void TrySignal_Twice_RunsCallbackEachTime()
    {
        // Arrange
        var name = UniqueName();
        using var received = new SemaphoreSlim(0);
        using var signal = new InstanceSignal(name, () => received.Release());

        // Act
        InstanceSignal.TrySignal(name);
        var first = received.Wait(SignalTimeout, TestContext.Current.CancellationToken);
        InstanceSignal.TrySignal(name);
        var second = received.Wait(SignalTimeout, TestContext.Current.CancellationToken);

        // Assert
        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    /// <summary>
    /// With nobody listening there is nobody to signal.
    /// </summary>
    [Fact]
    public void TrySignal_NobodyListening_ReturnsFalse()
    {
        // Act
        var sent = InstanceSignal.TrySignal(UniqueName());

        // Assert
        sent.Should().BeFalse();
    }

    /// <summary>
    /// Once disposed, the instance no longer listens.
    /// </summary>
    [Fact]
    public void TrySignal_AfterDispose_ReturnsFalse()
    {
        // Arrange
        var name = UniqueName();
        new InstanceSignal(name, () => { }).Dispose();

        // Act
        var sent = InstanceSignal.TrySignal(name);

        // Assert
        sent.Should().BeFalse();
    }

    /// <summary>
    /// Makes an event name no other test or process uses.
    /// </summary>
    /// <returns>A session-local event name.</returns>
    private static string UniqueName()
    {
        return $@"Local\DeskBuddy.Tests.{Guid.NewGuid():N}";
    }
}
