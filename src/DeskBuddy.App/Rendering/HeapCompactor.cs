using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Gives memory back to the system after something that held a lot of it has let go, such as the panel's preview pictures,
/// and from time to time while the application sits idle. The runtime collects when it needs room, not when something is
/// freed, and Windows keeps every page a process has touched in its working set until it is short of memory, so without this
/// the numbers in the task manager stay at the highest the application has been (measured: about 230 MB shown for a program
/// that touches about 30 MB of it when it is idle). Pages that are still in use come back at once, at the cost of a minor
/// page fault each.
/// </summary>
public sealed partial class HeapCompactor
{
    /// <summary>
    /// How long a request waits before it collects, so that the things that are being let go of are really unreferenced
    /// and several requests that come close together cost one collection.
    /// </summary>
    internal static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The collection, which is replaced in tests.
    /// </summary>
    private readonly Action _collect;

    /// <summary>
    /// The trimming of the working set that follows the collection, which is replaced in tests.
    /// </summary>
    private readonly Action _trim;

    /// <summary>
    /// How long a request waits before it collects.
    /// </summary>
    private readonly TimeSpan _delay;

    /// <summary>
    /// Whether a collection is already waiting to run (0 or 1), so requests that come meanwhile are folded into it.
    /// </summary>
    private int _pending;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeapCompactor"/> class that collects the whole heap, large objects
    /// included, and then trims the working set of the process.
    /// </summary>
    public HeapCompactor()
        : this(CollectAndCompact, TrimWorkingSet, DefaultDelay)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HeapCompactor"/> class with its own collection and trimming.
    /// </summary>
    /// <param name="collect">The collection to run.</param>
    /// <param name="trim">The trimming to run after the collection.</param>
    /// <param name="delay">How long a request waits before it collects.</param>
    internal HeapCompactor(Action collect, Action trim, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(collect);
        ArgumentNullException.ThrowIfNull(trim);

        _collect = collect;
        _trim = trim;
        _delay = delay;
    }

    /// <summary>
    /// Asks for a collection soon, on a thread-pool thread so that the caller is not held up. Requests made while one is
    /// waiting are folded into it.
    /// </summary>
    /// <returns>A task that completes when the collection that serves this request has run.</returns>
    public Task RequestAsync()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            await Task.Delay(_delay).ConfigureAwait(false);
            Volatile.Write(ref _pending, 0);
            _collect();
            _trim();
        });
    }

    /// <summary>
    /// Asks for a collection once after a first wait and then again and again at an interval, until stopped. The pet may be
    /// busy at any of these moments; a collection of a heap this small takes a few milliseconds, and the pages that
    /// the pet needs are back in the working set as soon as it touches them.
    /// </summary>
    /// <param name="firstAfter">How long to wait before the first request, so that start-up is over.</param>
    /// <param name="every">How long to wait between the requests after that.</param>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <param name="logger">
    /// Reports a collection that failed, which does not end the loop; <see langword="null"/> reports nothing.
    /// </param>
    /// <returns>A task that completes when the loop has been stopped; it does not fail.</returns>
    public async Task RunPeriodicallyAsync(
        TimeSpan firstAfter,
        TimeSpan every,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(every, TimeSpan.Zero);

        try
        {
            await Task.Delay(firstAfter, cancellationToken).ConfigureAwait(false);
            while (true)
            {
                try
                {
                    await RequestAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Giving memory back is a nicety: one that failed is reported, and the next one is tried at its time.
                    logger?.LogWarning(ex, "Giving memory back to the system failed.");
                }

                await Task.Delay(every, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped: the application is closing.
        }
    }

    /// <summary>
    /// Collects every generation, compacting the large object heap once, where the decoded atlases of pets are.
    /// </summary>
    private static void CollectAndCompact()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    /// <summary>
    /// Asks Windows to take the pages that are not in use out of the working set of this process.
    /// </summary>
    private static void TrimWorkingSet()
    {
        using var process = Process.GetCurrentProcess();

        // A failure only means the numbers stay where they were, so it is not reported.
        _ = SetProcessWorkingSetSize(process.Handle, -1, -1);
    }

    /// <summary>
    /// Sets the limits of the working set of a process; a minimum and maximum of -1 trim it as far as possible.
    /// </summary>
    /// <param name="process">The handle of the process.</param>
    /// <param name="minimum">The minimum size in bytes, or -1.</param>
    /// <param name="maximum">The maximum size in bytes, or -1.</param>
    /// <returns><see langword="true"/> if it worked.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(nint process, nint minimum, nint maximum);
}
