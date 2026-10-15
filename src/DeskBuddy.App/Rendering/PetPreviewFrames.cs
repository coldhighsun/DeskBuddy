using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Supplies the pictures shown in the pet previews. Unlike <see cref="PetFrameCache"/> it does not decode every picture of
/// a pet or build hit-test masks, as the panel may show many pets at once. The pictures a preview is going to need are
/// cut out ahead of time on a worker thread of its own (<see cref="PreloadAsync"/>), so the UI thread does not decode an
/// atlas.
/// </summary>
public sealed class PetPreviewFrames : IDisposable
{
    /// <summary>
    /// How long <see cref="Dispose"/> waits for the worker thread to finish what it is doing.
    /// </summary>
    private static readonly TimeSpan WorkerShutdownTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Guards the dictionaries, which the UI thread and the worker thread use.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// The pictures cut out so far, by name (see <see cref="FrameId"/>); <see langword="null"/> for one that could not be
    /// read. A failure is remembered so that a preview does not try again at every frame, but a <see cref="PreloadAsync"/>
    /// forgets it and tries again: it is called when a pet is loaded, which is when a repaired pet is to be picked up.
    /// </summary>
    private readonly Dictionary<string, BitmapSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The image files decoded so far by path (the atlases that the pictures are areas of), held weakly: a decoded atlas is
    /// several megabytes, of which a preview needs a few cells, so it is kept only while it is in use and decoded again
    /// if it was collected.
    /// </summary>
    private readonly Dictionary<string, WeakReference<BitmapSource>> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The image files that could not be read, so that the cells of such an atlas are not each tried again; forgotten, like
    /// the failed pictures, when a <see cref="PreloadAsync"/> asks for a picture of the file.
    /// </summary>
    private readonly HashSet<string> _unreadable = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One lock for each image file, held while the file is decoded, so that two threads that need the same atlas at the
    /// same time decode it once: the second waits for the first and then finds it.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lock> _decoding = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Decodes an image file; <see cref="FrameImages.Load"/>, except in tests, which count the decodes.
    /// </summary>
    private readonly Func<string, BitmapSource> _load;

    /// <summary>
    /// The requests for pictures to be cut out ahead of time, which the worker thread takes one at a time.
    /// </summary>
    private readonly BlockingCollection<(IReadOnlyList<string> Paths, TaskCompletionSource Done)> _requests = [];

    /// <summary>
    /// Whether <see cref="Dispose"/> has been called, after which no worker is started and nothing is queued.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Whether the worker is to stop: set by <see cref="Dispose"/>, after which the worker finishes the picture or atlas it
    /// is on, skips the rest of what is queued (completing the requests, as nothing needs the pictures any more) and ends.
    /// </summary>
    private volatile bool _stopping;

    /// <summary>
    /// The thread that cuts out the pictures that were asked for ahead of time; <see langword="null"/> until the first
    /// request that has anything to do.
    /// </summary>
    private Thread? _worker;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetPreviewFrames"/> class.
    /// </summary>
    public PetPreviewFrames()
        : this(FrameImages.Load)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PetPreviewFrames"/> class that decodes image files in its own way.
    /// </summary>
    /// <param name="load">
    /// Decodes an image file.
    /// </param>
    internal PetPreviewFrames(Func<string, BitmapSource> load)
    {
        ArgumentNullException.ThrowIfNull(load);

        _load = load;
    }

    /// <summary>
    /// Gets a picture, cutting it out of its file if that has not been done yet.
    /// </summary>
    /// <param name="path">
    /// The name of the picture: a file path, with the area of the file if the picture is only a part of it (see
    /// <see cref="FrameId"/>).
    /// </param>
    /// <returns>
    /// The frozen picture, or <see langword="null"/> if the file cannot be read as an image.
    /// </returns>
    public ImageSource? Get(string path)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(path, out var cached))
            {
                return cached;
            }
        }

        return Cut(path);
    }

    /// <summary>
    /// Cuts out the given pictures on a worker thread, so that the caller's thread (the UI thread) is not held up by
    /// decoding the atlas they are in. There is one worker for all requests, so a picture that two requests ask for is cut
    /// out once, and the atlas is not decoded twice at the same time. The pictures are frozen, which is what lets any thread
    /// use them afterwards. The worker is a thread made for the purpose rather than a thread-pool one, which would keep a
    /// WPF dispatcher alive for every pool thread it decodes on. A picture that cannot be read is left as a gap, as
    /// <see cref="Get"/> does; one that failed before is tried again.
    /// </summary>
    /// <param name="paths">
    /// The names of the pictures.
    /// </param>
    /// <returns>
    /// A task that completes when the pictures are ready; it completes at once if they all are already.
    /// </returns>
    public Task PreloadAsync(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        List<string> missing = [];
        lock (_gate)
        {
            foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (_cache.TryGetValue(path, out var cached))
                {
                    if (cached is not null)
                    {
                        continue;
                    }

                    // A failure from before: forget it, so that a pet that has been repaired since is picked up.
                    _cache.Remove(path);
                }

                // A file may have been marked unreadable by the failure of some other picture of it; whatever pictures of it
                // are asked for now, it gets another try.
                _unreadable.Remove(FrameId.Parse(path).Path);
                missing.Add(path);
            }

            if (missing.Count == 0 || _disposed)
            {
                return Task.CompletedTask;
            }

            if (_worker is null)
            {
                _worker = new Thread(RunWorker)
                {
                    IsBackground = true,
                    Name = "Pet preview decoding",
                };
                _worker.SetApartmentState(ApartmentState.STA);
                _worker.Start();
            }
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _requests.Add((missing, done));
        }
        catch (InvalidOperationException)
        {
            // Disposed: nothing is prepared ahead any more; Get still cuts a picture out when it is asked for.
            return Task.CompletedTask;
        }

        return done.Task;
    }

    /// <summary>
    /// Stops the worker thread, if there is one: it finishes the picture it is on (an atlas that is being decoded cannot be
    /// interrupted), skips the rest of the queue, and is waited for a short time. Pictures can still be had
    /// with <see cref="Get"/>, but none is prepared ahead of time any more.
    /// </summary>
    public void Dispose()
    {
        Thread? worker;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopping = true;
            worker = _worker;
        }

        _requests.CompleteAdding();

        // The worker finishes the request it is on and the ones queued, and then ends; wait for it, but not for long, as
        // this is called as the application exits. The queue is only disposed once nothing can be using it any more.
        if (worker is null || worker.Join(WorkerShutdownTimeout))
        {
            _requests.Dispose();
        }
    }

    /// <summary>
    /// Tells whether an exception means that a picture cannot be read from its file, as against a bug: the failures that
    /// <see cref="PetLoadFailure.Is"/> counts as a pet that cannot be used, and the one WPF raises for an image it cannot
    /// make sense of.
    /// </summary>
    /// <param name="exception">
    /// The exception.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the file is at fault.
    /// </returns>
    internal static bool IsUnreadable(Exception exception)
    {
        return PetLoadFailure.Is(exception) || exception is InvalidOperationException;
    }

    /// <summary>
    /// The worker thread: cuts out the pictures of one request after the other until the cache is disposed.
    /// </summary>
    private void RunWorker()
    {
        foreach (var (paths, done) in _requests.GetConsumingEnumerable())
        {
            if (_stopping)
            {
                done.TrySetResult();
                continue;
            }

            try
            {
                foreach (var file in paths.GroupBy(path => FrameId.Parse(path).Path, StringComparer.OrdinalIgnoreCase))
                {
                    if (_stopping)
                    {
                        break;
                    }

                    // The atlas is held in a variable while its pictures are cut out, as the cache holds it only weakly and
                    // it could otherwise be collected, and decoded again, between two pictures.
                    var atlas = HoldFile(file.Key);
                    foreach (var path in file)
                    {
                        if (_stopping)
                        {
                            break;
                        }

                        // A picture that an earlier request, or the UI thread, has cut out in the meantime is not cut again.
                        Get(path);
                    }

                    GC.KeepAlive(atlas);
                }

                done.SetResult();
            }
            catch (Exception ex)
            {
                // Not handled here: the error is handed on to whoever awaits the task.
                done.SetException(ex);
            }
        }
    }

    /// <summary>
    /// Decodes an image file ahead of cutting its pictures out, noting it as unreadable if it cannot be.
    /// </summary>
    /// <param name="file">
    /// The path of the file.
    /// </param>
    /// <returns>
    /// The image, or <see langword="null"/> if it cannot be read.
    /// </returns>
    private BitmapSource? HoldFile(string file)
    {
        try
        {
            var image = LoadFile(file);
            lock (_gate)
            {
                _unreadable.Remove(file);
            }

            return image;
        }
        catch (Exception ex) when (IsUnreadable(ex))
        {
            lock (_gate)
            {
                _unreadable.Add(file);
            }

            return null;
        }
    }

    /// <summary>
    /// Cuts a picture out of its file and remembers it.
    /// </summary>
    /// <param name="path">
    /// The name of the picture.
    /// </param>
    /// <returns>
    /// The picture, or <see langword="null"/> if it cannot be read.
    /// </returns>
    private BitmapSource? Cut(string path)
    {
        var (file, _) = FrameId.Parse(path);
        BitmapSource? picture = null;
        lock (_gate)
        {
            if (_unreadable.Contains(file))
            {
                return Remember(path, null);
            }
        }

        try
        {
            picture = FrameImages.Cut(path, LoadFile);
        }
        catch (Exception ex) when (IsUnreadable(ex))
        {
            // A picture that cannot be decoded, or an area outside the image, only leaves a gap in the preview (the pet itself
            // reports such a file when it is chosen); none of the other pictures of the file is tried either.
            lock (_gate)
            {
                _unreadable.Add(file);
            }
        }

        return Remember(path, picture);
    }

    /// <summary>
    /// Remembers the picture of a name, or that it could not be read. There is one picture per name, whoever asks: if
    /// another thread has cut the picture out meanwhile, that one is kept and returned, and a failure never replaces a
    /// picture, though a picture replaces a failure.
    /// </summary>
    /// <param name="path">
    /// The name of the picture.
    /// </param>
    /// <param name="picture">
    /// The picture, or <see langword="null"/> if it could not be read.
    /// </param>
    /// <returns>
    /// The picture that is kept for the name, or <see langword="null"/> if there is none.
    /// </returns>
    internal BitmapSource? Remember(string path, BitmapSource? picture)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(path, out var existing) && existing is not null)
            {
                return existing;
            }

            _cache[path] = picture;
            return picture;
        }
    }

    /// <summary>
    /// Gets a decoded image file, decoding it if it has not been decoded yet or was collected since.
    /// </summary>
    /// <param name="file">
    /// The path of the file.
    /// </param>
    /// <returns>
    /// The image.
    /// </returns>
    private BitmapSource LoadFile(string file)
    {
        if (TryGetLive(file) is { } alive)
        {
            return alive;
        }

        // Only one thread at a time decodes a file; one that arrives meanwhile waits and finds the result. The decoding is
        // done outside _gate, so the UI thread is not held up by anything but a decode of the very file it needs.
        lock (_decoding.GetOrAdd(file, _ => new Lock()))
        {
            if (TryGetLive(file) is { } decodedMeanwhile)
            {
                return decodedMeanwhile;
            }

            var image = _load(file);
            lock (_gate)
            {
                _files[file] = new WeakReference<BitmapSource>(image);
            }

            return image;
        }
    }

    /// <summary>
    /// Gets a decoded image file if it has been decoded and not collected since.
    /// </summary>
    /// <param name="file">
    /// The path of the file.
    /// </param>
    /// <returns>
    /// The image, or <see langword="null"/> if there is none.
    /// </returns>
    private BitmapSource? TryGetLive(string file)
    {
        lock (_gate)
        {
            return _files.TryGetValue(file, out var weak) && weak.TryGetTarget(out var alive) ? alive : null;
        }
    }
}
