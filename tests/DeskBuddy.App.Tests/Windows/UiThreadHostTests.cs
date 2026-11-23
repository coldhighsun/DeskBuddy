using System.Windows;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests.Windows;

/// <summary>
/// Tests for <see cref="UiThreadHost{T}"/>, which runs a window on a UI thread of its own. The windows are plain ones that
/// are never shown.
/// </summary>
public sealed class UiThreadHostTests
{
    /// <summary>
    /// How long a test waits for the host's thread before it gives up.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The window is made on the host's own thread, not on the caller's.
    /// </summary>
    [Fact]
    public async Task Started_Always_CompletesAfterTheWindowWasMadeOnAnotherThread()
    {
        // Arrange
        var madeOn = 0;
        using var host = new UiThreadHost<Window>(
            "test",
            () =>
            {
                madeOn = Environment.CurrentManagedThreadId;
                return new Window();
            },
            _ => true);

        // Act
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        madeOn.Should().NotBe(0).And.NotBe(Environment.CurrentManagedThreadId);
    }

    /// <summary>
    /// Posted work runs on the thread of the window, with the window.
    /// </summary>
    [Fact]
    public async Task Post_Work_RunsOnTheWindowThreadWithTheWindow()
    {
        // Arrange
        using var host = Create(out var window);
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);
        var ran = new TaskCompletionSource<(int Thread, Window? Window)>();

        // Act
        host.Post(w => ran.SetResult((Environment.CurrentManagedThreadId, w)));
        var (thread, seen) = await ran.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        thread.Should().NotBe(Environment.CurrentManagedThreadId);
        seen.Should().BeSameAs(window());
    }

    /// <summary>
    /// Work that does not need the window runs on the same thread.
    /// </summary>
    [Fact]
    public async Task Post_WorkWithoutWindow_RunsOnTheWindowThread()
    {
        // Arrange
        using var host = Create(out _);
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);
        var ran = new TaskCompletionSource<int>();

        // Act
        host.Post(() => ran.SetResult(Environment.CurrentManagedThreadId));

        // Assert
        (await ran.Task.WaitAsync(Patience, TestContext.Current.CancellationToken)).Should().NotBe(Environment.CurrentManagedThreadId);
    }

    /// <summary>
    /// Work that returns something gives it back to the caller, and the caller's thread is not held while it runs.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Function_ReturnsWhatItComputedOnTheWindowThread()
    {
        // Arrange
        using var host = Create(out _);
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Act
        var thread = await host.InvokeAsync(_ => Environment.CurrentManagedThreadId).WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        thread.Should().NotBe(Environment.CurrentManagedThreadId);
    }

    /// <summary>
    /// Asynchronous work is waited for to its end.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AsyncWork_CompletesWhenTheWorkHas()
    {
        // Arrange
        using var host = Create(out _);
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);
        var finished = false;

        // Act
        await host.InvokeAsync(async _ =>
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            finished = true;
        }).WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        finished.Should().BeTrue();
    }

    /// <summary>
    /// A window that cannot be made is reported through <see cref="UiThreadHost{T}.Started"/>.
    /// </summary>
    [Fact]
    public async Task Started_WindowCannotBeMade_FailsWithTheError()
    {
        // Arrange
        using var host = new UiThreadHost<Window>(
            "test",
            () => throw new InvalidOperationException("no window"),
            _ => true);

        // Act
        var act = () => host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("no window");
    }

    /// <summary>
    /// A host whose window could not be made takes no more work: the dispatcher is refused, and nothing is queued on the
    /// thread that has ended.
    /// </summary>
    [Fact]
    public async Task Dispatcher_WindowCannotBeMade_IsRefused()
    {
        // Arrange
        using var host = new UiThreadHost<Window>(
            "test",
            () => throw new InvalidOperationException("no window"),
            _ => true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken));

        // Act
        var act = () => host.Dispatcher;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Work handed to a host whose window could not be made does not wait for good: it fails at once, and posting does
    /// nothing.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WindowCannotBeMade_FailsAtOnce()
    {
        // Arrange
        using var host = new UiThreadHost<Window>(
            "test",
            () => throw new InvalidOperationException("no window"),
            _ => true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken));

        // Act
        host.Post(() => throw new InvalidOperationException("never runs"));
        var act = () => host.InvokeAsync(_ => 1).WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// What runs first on the thread, before the window is made, is run there.
    /// </summary>
    [Fact]
    public async Task Started_PrepareGiven_RunsItBeforeTheWindowIsMade()
    {
        // Arrange
        var order = new List<string>();
        using var host = new UiThreadHost<Window>(
            "test",
            () =>
            {
                order.Add("create");
                return new Window();
            },
            _ => true,
            () => order.Add("prepare"));

        // Act
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        order.Should().Equal("prepare", "create");
    }

    /// <summary>
    /// An error nobody caught goes to the handler, and the thread carries on when it says so.
    /// </summary>
    [Fact]
    public async Task Post_WorkThrows_GoesToTheHandlerAndTheThreadCarriesOn()
    {
        // Arrange
        var handled = new TaskCompletionSource<Exception>();
        using var host = new UiThreadHost<Window>(
            "test",
            () => new Window(),
            exception =>
            {
                handled.TrySetResult(exception);
                return true;
            });
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Act
        host.Post(() => throw new InvalidOperationException("boom"));
        var error = await handled.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        var after = await host.InvokeAsync(_ => 42).WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Assert
        error.Message.Should().Be("boom");
        after.Should().Be(42);
    }

    /// <summary>
    /// Shutting down closes the window and ends the thread.
    /// </summary>
    [Fact]
    public async Task Shutdown_Running_ClosesTheWindowAndEndsTheThread()
    {
        // Arrange
        var closed = new TaskCompletionSource();
        using var host = new UiThreadHost<Window>(
            "test",
            () =>
            {
                // Shown far off screen and without taking the focus, so that running the tests does not disturb anyone.
                var window = new Window
                {
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Width = 1,
                    Height = 1,
                    Left = -10000,
                    Top = -10000,
                };
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                return window;
            },
            _ => true);
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Act
        host.Shutdown(Patience);

        // Assert
        await closed.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        host.Dispatcher.HasShutdownStarted.Should().BeTrue();
    }

    /// <summary>
    /// Work posted after shutdown is dropped without an error.
    /// </summary>
    [Fact]
    public async Task Post_AfterShutdown_DoesNothing()
    {
        // Arrange
        using var host = Create(out _);
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);
        host.Shutdown(Patience);
        var ran = false;

        // Act
        host.Post(() => ran = true);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        ran.Should().BeFalse();
    }

    /// <summary>
    /// The dispatcher cannot be had before the thread has started.
    /// </summary>
    [Fact]
    public async Task Dispatcher_AfterStarted_IsTheDispatcherOfAnotherThread()
    {
        // Arrange
        using var host = Create(out _);
        await host.Started.WaitAsync(Patience, TestContext.Current.CancellationToken);

        // Act
        var dispatcher = host.Dispatcher;

        // Assert
        dispatcher.CheckAccess().Should().BeFalse();
    }

    /// <summary>
    /// Makes a host whose window is a plain one.
    /// </summary>
    /// <param name="window">Gives the window that was made, once the host has started.</param>
    /// <returns>The host.</returns>
    private static UiThreadHost<Window> Create(out Func<Window?> window)
    {
        Window? made = null;
        window = () => made;
        return new UiThreadHost<Window>(
            "test",
            () => made = new Window(),
            _ => true);
    }
}
