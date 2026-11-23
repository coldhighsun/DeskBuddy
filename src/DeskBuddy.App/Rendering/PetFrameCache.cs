using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Pre-decodes every frame of a pet into frozen bitmaps so rendering never allocates or touches the disk.
/// </summary>
public sealed class PetFrameCache
{
    /// <summary>
    /// The longest edge in pixels of a hit-test mask. A frame is drawn at a small fraction of its atlas cell, so a mask
    /// at the resolution of the cell would spend most of its memory on detail that a pointer cannot hit.
    /// </summary>
    private const int MaskMaxEdge = 64;

    /// <summary>
    /// Decoded frames keyed by their name (see <see cref="FrameId"/>).
    /// </summary>
    private readonly Dictionary<string, BitmapSource> _frames;

    /// <summary>
    /// Alpha masks keyed by frame name, used for click-through hit testing.
    /// </summary>
    private readonly Dictionary<string, AlphaMask> _masks;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetFrameCache"/> class and decodes all frames of the pet.
    /// </summary>
    /// <param name="pet">The pet whose frames are decoded.</param>
    /// <param name="displayScale">
    /// The scaling of the largest monitor (1 is 100 %) the frames have to be sharp at, or <see langword="null"/> to read it
    /// from the monitors that are connected; see <see cref="DisplayEdgeCap"/>.
    /// </param>
    public PetFrameCache(Pet pet, double? displayScale = null)
    {
        ArgumentNullException.ThrowIfNull(pet);

        var paths = pet.Animations.Values
            .Concat(pet.GazeAnimations.Values)
            .SelectMany(animation => animation.FrameIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _frames = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        _masks = new Dictionary<string, AlphaMask>(StringComparer.OrdinalIgnoreCase);

        // The frames are areas of the atlas, which is decoded once, however many frames it holds.
        var files = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        var maxEdge = DisplayEdgeCap(pet, displayScale ?? DisplayScale.Current());
        foreach (var path in paths)
        {
            var bitmap = FrameImages.Cut(
                path,
                file =>
                {
                    if (!files.TryGetValue(file, out var image))
                    {
                        image = FrameImages.Load(file);
                        files[file] = image;
                    }

                    return image;
                },
                maxEdge);
            _frames[path] = bitmap;
            _masks[path] = BuildMask(bitmap);
        }
    }

    /// <summary>
    /// Works out the longest edge in pixels a frame of a pet needs to have, however it is shown: the biggest size the user
    /// can choose, at the scaling of the monitor with the largest scaling. A pet may draw its frames in cells several times
    /// that (the shipped pets draw them four times as large as they are shown at the middle size), and keeping every
    /// pixel costs memory for detail that is never on screen. It depends only on the frame size the pet declares and on the
    /// monitors, so a pet of any shape gets what it needs; on a monitor with a high scaling it may be as large as the cell,
    /// in which case nothing is scaled down.
    /// </summary>
    /// <param name="pet">The pet.</param>
    /// <param name="displayScale">The scaling of the largest monitor (1 is 100 %); kept between 1 and 3.</param>
    /// <returns>The longest edge in pixels; a frame of a larger cell is scaled down to it.</returns>
    internal static int DisplayEdgeCap(Pet pet, double displayScale)
    {
        ArgumentNullException.ThrowIfNull(pet);

        var longest = Math.Max(pet.FrameWidth, pet.FrameHeight);
        var biggest = Enum.GetValues<PetSize>().Max(size => size.Scale(longest));
        return (int)Math.Ceiling(biggest * DisplayScale.Clamp(displayScale));
    }

    /// <summary>
    /// Decodes the pictures of a pet on a thread of its own, so that the caller's thread is not held up. The pictures are
    /// frozen, which is what lets the thread that asked use them. A thread is made for each pet rather than borrowing a
    /// thread-pool one, which would keep a WPF dispatcher alive for every pool thread it decodes on.
    /// </summary>
    /// <param name="pet">The pet.</param>
    /// <param name="displayScale">
    /// The scaling of the largest monitor the frames have to be sharp at, or <see langword="null"/> to read it from the
    /// monitors that are connected.
    /// </param>
    /// <returns>
    /// A task that completes with the cache, or fails with the error that made a picture undecodable.
    /// </returns>
    public static Task<PetFrameCache> CreateAsync(Pet pet, double? displayScale = null)
    {
        var completion = new TaskCompletionSource<PetFrameCache>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(new PetFrameCache(pet, displayScale));
            }
            catch (Exception ex)
            {
                // Not handled here: the error is handed on to whoever awaits the task.
                completion.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Pet frame decoding",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>
    /// Gets the decoded frame for a file path.
    /// </summary>
    /// <param name="path">Frame file path as listed in the pet.</param>
    /// <returns>The frozen, decoded image.</returns>
    public ImageSource Get(string path)
    {
        return _frames[path];
    }

    /// <summary>
    /// Gets the alpha mask for a frame.
    /// </summary>
    /// <param name="path">Frame file path as listed in the pet.</param>
    /// <returns>The mask describing which pixels of the frame are opaque.</returns>
    public AlphaMask GetMask(string path)
    {
        return _masks[path];
    }

    /// <summary>
    /// Extracts the alpha channel of a bitmap.
    /// </summary>
    /// <param name="bitmap">The decoded bitmap, in any pixel format.</param>
    /// <returns>The alpha mask of the bitmap.</returns>
    private static AlphaMask BuildMask(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);
        return AlphaMask.FromBgra32(width, height, pixels, stride, MaskMaxEdge);
    }
}
