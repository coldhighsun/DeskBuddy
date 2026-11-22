using DeskBuddy.Infrastructure.Sensors;

namespace DeskBuddy.Infrastructure.Tests.Sensors;

/// <summary>
/// Tests for <see cref="WinEventForegroundHook"/>, against the real window system of the machine, which needs a desktop
/// session like the other tests that open windows. Which window is in front cannot be chosen here, so the tests look
/// at what holds whatever it is.
/// </summary>
public class WinEventForegroundHookTests
{
    /// <summary>
    /// How long to wait for the hook's thread to do something before the test gives up.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The hook installs itself, and soon after the start publishes the program that is in front, so nobody has to wait
    /// for a window change.
    /// </summary>
    [Fact]
    public async Task Constructor_InstallsTheHookAndPublishesTheCurrentProgram()
    {
        // Arrange
        var published = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        using var hook = new WinEventForegroundHook(name => published.TrySetResult(name), NullLogger<WinEventForegroundHook>.Instance);
        var done = await Task.WhenAny(published.Task, Task.Delay(Patience, TestContext.Current.CancellationToken));

        // Assert
        hook.IsInstalled.Should().BeTrue();
        hook.IsActive.Should().BeTrue();
        done.Should().BeSameAs(published.Task);
    }

    /// <summary>
    /// Removing the hook ends its thread, leaves it uninstalled, and may be done twice.
    /// </summary>
    [Fact]
    public void Dispose_RemovesTheHookAndMayBeRepeated()
    {
        // Arrange
        var hook = new WinEventForegroundHook(_ => { }, NullLogger<WinEventForegroundHook>.Instance);

        // Act
        hook.Dispose();
        var again = () => hook.Dispose();

        // Assert
        hook.IsInstalled.Should().BeFalse();
        hook.IsActive.Should().BeFalse();
        again.Should().NotThrow();
    }

    /// <summary>
    /// A listener that throws on the first publish is logged and does not end the process, as that publish runs on the
    /// hook's own thread, where nothing catches.
    /// </summary>
    [Fact]
    public async Task Constructor_WhenFirstPublishThrows_DoesNotCrashAndKeepsHook()
    {
        // Arrange
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        using var hook = new WinEventForegroundHook(
            _ =>
            {
                called.TrySetResult();
                throw new InvalidOperationException("listener");
            },
            NullLogger<WinEventForegroundHook>.Instance);
        var done = await Task.WhenAny(called.Task, Task.Delay(Patience, TestContext.Current.CancellationToken));

        // Assert
        done.Should().BeSameAs(called.Task);
        hook.IsInstalled.Should().BeTrue();
        hook.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// Removing a hook that was replaced by a newer one leaves the newer one in charge of the system's events.
    /// </summary>
    [Fact]
    public void Dispose_OfReplacedHook_KeepsTheNewerHookActive()
    {
        // Arrange
        var older = new WinEventForegroundHook(_ => { }, NullLogger<WinEventForegroundHook>.Instance);
        using var newer = new WinEventForegroundHook(_ => { }, NullLogger<WinEventForegroundHook>.Instance);

        // Act
        var olderActiveBefore = older.IsActive;
        older.Dispose();

        // Assert
        olderActiveBefore.Should().BeFalse();
        newer.IsInstalled.Should().BeTrue();
        newer.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// Hooks that are made and removed again and again end their threads without throwing on any of them.
    /// </summary>
    [Fact]
    public void Dispose_RepeatedCreateAndDispose_DoesNotThrow()
    {
        // Act
        var cycle = () =>
        {
            for (var round = 0; round < 25; round++)
            {
                new WinEventForegroundHook(_ => { }, NullLogger<WinEventForegroundHook>.Instance).Dispose();
            }
        };

        // Assert
        cycle.Should().NotThrow();
    }

    /// <summary>
    /// A hook that was removed can be installed again, as the application may be started again in the same process.
    /// </summary>
    [Fact]
    public void Constructor_AfterAnotherHookWasDisposed_Works()
    {
        // Arrange
        new WinEventForegroundHook(_ => { }, NullLogger<WinEventForegroundHook>.Instance).Dispose();

        // Act
        using var second = new WinEventForegroundHook(_ => { }, NullLogger<WinEventForegroundHook>.Instance);

        // Assert
        second.IsInstalled.Should().BeTrue();
        second.IsActive.Should().BeTrue();
    }
}
