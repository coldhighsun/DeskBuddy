using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Pre-decodes every frame of a pet into frozen bitmaps so rendering never allocates or touches the disk.
/// </summary>
public sealed class PetFrameCache
{
    /// <summary>
    /// Decoded frames keyed by their file path.
    /// </summary>
    private readonly Dictionary<string, BitmapSource> _frames;

    /// <summary>
    /// Alpha masks keyed by frame file path, used for click-through hit testing.
    /// </summary>
    private readonly Dictionary<string, AlphaMask> _masks;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetFrameCache"/> class and decodes all frames of the pet.
    /// </summary>
    /// <param name="pet">
    /// The pet whose frames are decoded.
    /// </param>
    public PetFrameCache(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        var paths = pet.Animations.Values
            .Concat(pet.GazeAnimations.Values)
            .SelectMany(animation => animation.FramePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _frames = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        _masks = new Dictionary<string, AlphaMask>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var bitmap = Decode(path);
            _frames[path] = bitmap;
            _masks[path] = BuildMask(bitmap);
        }
    }

    /// <summary>
    /// Decodes the pictures of a pet on a thread of its own, so that the caller's thread is not held up. The pictures are
    /// frozen, which is what lets the thread that asked use them. A thread is made for each pet rather than borrowing a
    /// thread-pool one, which would keep a WPF dispatcher alive for every pool thread it decodes on.
    /// </summary>
    /// <param name="pet">
    /// The pet.
    /// </param>
    /// <returns>
    /// A task that completes with the cache, or fails with the error that made a picture undecodable.
    /// </returns>
    public static Task<PetFrameCache> CreateAsync(Pet pet)
    {
        var completion = new TaskCompletionSource<PetFrameCache>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(new PetFrameCache(pet));
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
    /// <param name="path">
    /// Frame file path as listed in the pet.
    /// </param>
    /// <returns>
    /// The frozen, decoded image.
    /// </returns>
    public ImageSource Get(string path)
    {
        return _frames[path];
    }

    /// <summary>
    /// Gets the alpha mask for a frame.
    /// </summary>
    /// <param name="path">
    /// Frame file path as listed in the pet.
    /// </param>
    /// <returns>
    /// The mask describing which pixels of the frame are opaque.
    /// </returns>
    public AlphaMask GetMask(string path)
    {
        return _masks[path];
    }

    /// <summary>
    /// Extracts the alpha channel of a bitmap.
    /// </summary>
    /// <param name="bitmap">
    /// The decoded bitmap, in any pixel format.
    /// </param>
    /// <returns>
    /// The alpha mask of the bitmap.
    /// </returns>
    private static AlphaMask BuildMask(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);
        return AlphaMask.FromBgra32(width, height, pixels, stride);
    }

    /// <summary>
    /// Decodes an image file into a frozen bitmap, fully loaded so the file handle is released.
    /// </summary>
    /// <param name="path">
    /// Image file path.
    /// </param>
    /// <returns>
    /// The frozen bitmap.
    /// </returns>
    private static BitmapImage Decode(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
