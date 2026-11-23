using DeskBuddy.App.Rendering;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Tests.Rendering;

/// <summary>
/// Tests for <see cref="HeapCompactor"/>, with a collection and a trimming that only count.
/// </summary>
public class HeapCompactorTests
{
    /// <summary>
    /// A request runs the collection once, after the delay.
    /// </summary>
    [Fact]
    public async Task RequestAsync_OneRequest_CollectsOnce()
    {
        // Arrange
        var collections = 0;
        var compactor = new HeapCompactor(() => Interlocked.Increment(ref collections), () => { }, TimeSpan.FromMilliseconds(20));

        // Act
        await compactor.RequestAsync();

        // Assert
        collections.Should().Be(1);
    }

    /// <summary>
    /// The working set is trimmed after the collection, not before it: the pages the collection frees are then among them.
    /// </summary>
    [Fact]
    public async Task RequestAsync_OneRequest_TrimsAfterCollecting()
    {
        // Arrange
        List<string> steps = [];
        var compactor = new HeapCompactor(() => steps.Add("collect"), () => steps.Add("trim"), TimeSpan.FromMilliseconds(10));

        // Act
        await compactor.RequestAsync();

        // Assert
        steps.Should().Equal("collect", "trim");
    }

    /// <summary>
    /// Requests that come while one is waiting are folded into it, so a burst costs one collection.
    /// </summary>
    [Fact]
    public async Task RequestAsync_BurstOfRequests_CollectsOnce()
    {
        // Arrange
        var collections = 0;
        var trims = 0;
        var compactor = new HeapCompactor(
            () => Interlocked.Increment(ref collections),
            () => Interlocked.Increment(ref trims),
            TimeSpan.FromMilliseconds(50));

        // Act
        var first = compactor.RequestAsync();
        await Task.WhenAll(compactor.RequestAsync(), compactor.RequestAsync(), compactor.RequestAsync());
        await first;

        // Assert
        collections.Should().Be(1);
        trims.Should().Be(1);
    }

    /// <summary>
    /// A request made after the previous collection ran is served by a new collection.
    /// </summary>
    [Fact]
    public async Task RequestAsync_AfterACollection_CollectsAgain()
    {
        // Arrange
        var collections = 0;
        var compactor = new HeapCompactor(() => Interlocked.Increment(ref collections), () => { }, TimeSpan.FromMilliseconds(10));
        await compactor.RequestAsync();

        // Act
        await compactor.RequestAsync();

        // Assert
        collections.Should().Be(2);
    }

    /// <summary>
    /// The real collection and trimming run without failing.
    /// </summary>
    [Fact]
    public async Task RequestAsync_DefaultCollection_DoesNotThrow()
    {
        // Arrange
        var compactor = new HeapCompactor();

        // Act
        var act = () => compactor.RequestAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// The periodic loop collects after the first wait and then again at each interval, and ends quietly when stopped.
    /// </summary>
    [Fact]
    public async Task RunPeriodicallyAsync_UntilStopped_CollectsRepeatedlyAndEndsWithoutFailing()
    {
        // Arrange
        var collections = 0;
        var compactor = new HeapCompactor(() => Interlocked.Increment(ref collections), () => { }, TimeSpan.FromMilliseconds(1));
        using var stop = new CancellationTokenSource();

        // Act
        var loop = compactor.RunPeriodicallyAsync(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(10), stop.Token);
        while (Volatile.Read(ref collections) < 3)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        await stop.CancelAsync();
        await loop;

        // Assert
        collections.Should().BeGreaterThanOrEqualTo(3);
    }

    /// <summary>
    /// A collection that fails is reported and the loop carries on, so one failure does not end the giving back of memory
    /// for the rest of the run.
    /// </summary>
    [Fact]
    public async Task RunPeriodicallyAsync_CollectionFailsOnce_IsReportedAndTheLoopCarriesOn()
    {
        // Arrange
        var calls = 0;
        var compactor = new HeapCompactor(
            () =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new InvalidOperationException("the first collection fails");
                }
            },
            () => { },
            TimeSpan.FromMilliseconds(1));
        var logger = new CapturingLogger();
        using var stop = new CancellationTokenSource();

        // Act
        var loop = compactor.RunPeriodicallyAsync(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(10), stop.Token, logger);
        while (Volatile.Read(ref calls) < 3)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        await stop.CancelAsync();
        await loop;

        // Assert
        calls.Should().BeGreaterThanOrEqualTo(3);
        logger.Warnings.Should().ContainSingle().Which.Should().BeOfType<InvalidOperationException>();
    }

    /// <summary>
    /// Stopping during the first wait ends the loop without a collection.
    /// </summary>
    [Fact]
    public async Task RunPeriodicallyAsync_StoppedBeforeTheFirstWait_NeverCollects()
    {
        // Arrange
        var collections = 0;
        var compactor = new HeapCompactor(() => Interlocked.Increment(ref collections), () => { }, TimeSpan.FromMilliseconds(1));
        using var stop = new CancellationTokenSource();

        // Act
        var loop = compactor.RunPeriodicallyAsync(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), stop.Token);
        await stop.CancelAsync();
        await loop;

        // Assert
        collections.Should().Be(0);
    }

    /// <summary>
    /// An interval that is not a length of time is a programming error.
    /// </summary>
    [Fact]
    public async Task RunPeriodicallyAsync_NonPositiveInterval_Throws()
    {
        // Arrange
        var compactor = new HeapCompactor(() => { }, () => { }, TimeSpan.Zero);

        // Act
        var act = () => compactor.RunPeriodicallyAsync(TimeSpan.Zero, TimeSpan.Zero, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// A logger that keeps the exceptions of the warnings it was given.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        /// <summary>
        /// Gets the exceptions that came with the warnings, in order.
        /// </summary>
        public List<Exception?> Warnings { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                lock (Warnings)
                {
                    Warnings.Add(exception);
                }
            }
        }
    }
}
