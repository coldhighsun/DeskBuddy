namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="SingleInstanceGuard"/>.
/// </summary>
public sealed class SingleInstanceGuardTests
{
    /// <summary>
    /// How long a test waits for the first instance to go away.
    /// </summary>
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The first guard on a name is the first instance; a later one is not.
    /// </summary>
    [Fact]
    public void IsFirstInstance_SecondGuard_IsFalse()
    {
        // Arrange
        var name = UniqueName();
        using var first = new SingleInstanceGuard(name);

        // Act
        using var second = new SingleInstanceGuard(name);

        // Assert
        first.IsFirstInstance.Should().BeTrue();
        second.IsFirstInstance.Should().BeFalse();
    }

    /// <summary>
    /// The first instance does not wait for itself.
    /// </summary>
    [Fact]
    public void WaitForFirstInstanceToExit_FirstInstance_ReturnsTrueAtOnce()
    {
        // Arrange
        using var guard = new SingleInstanceGuard(UniqueName());

        // Act
        var exited = guard.WaitForFirstInstanceToExit(TimeSpan.Zero);

        // Assert
        exited.Should().BeTrue();
    }

    /// <summary>
    /// A later instance waits while the first one keeps running.
    /// </summary>
    [Fact]
    public void WaitForFirstInstanceToExit_FirstStillRunning_TimesOut()
    {
        // Arrange
        var name = UniqueName();
        using var first = new SingleInstanceGuard(name);
        var exited = true;

        // Act: a mutex belongs to a thread, so the later instance waits on a thread of its own.
        var thread = new Thread(() =>
        {
            using var second = new SingleInstanceGuard(name);
            exited = second.WaitForFirstInstanceToExit(TimeSpan.FromMilliseconds(100));
        });
        thread.Start();
        thread.Join();

        // Assert
        exited.Should().BeFalse();
    }

    /// <summary>
    /// A later instance sees the first one exit once it has released the guard.
    /// </summary>
    [Fact]
    public void WaitForFirstInstanceToExit_FirstExits_ReturnsTrue()
    {
        // Arrange
        var name = UniqueName();
        using var firstReady = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var firstThread = new Thread(() =>
        {
            using var first = new SingleInstanceGuard(name);
            firstReady.Set();
            releaseFirst.Wait();
        });
        firstThread.Start();
        firstReady.Wait(TestContext.Current.CancellationToken);
        using var second = new SingleInstanceGuard(name);

        // Act
        releaseFirst.Set();
        var exited = second.WaitForFirstInstanceToExit(ExitTimeout);
        firstThread.Join();

        // Assert
        second.IsFirstInstance.Should().BeFalse();
        exited.Should().BeTrue();
    }

    /// <summary>
    /// Makes a mutex name no other test or process uses.
    /// </summary>
    /// <returns>A session-local mutex name.</returns>
    private static string UniqueName()
    {
        return $@"Local\DeskBuddy.Tests.{Guid.NewGuid():N}";
    }
}
