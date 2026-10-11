using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.Domain.Skins;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Pre-decodes every frame of a skin into frozen bitmaps so rendering never allocates or touches the disk.
/// </summary>
public sealed class SkinFrameCache
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
    /// Initializes a new instance of the <see cref="SkinFrameCache"/> class and decodes all frames of the skin.
    /// </summary>
    /// <param name="skin">
    /// The skin whose frames are decoded.
    /// </param>
    public SkinFrameCache(Skin skin)
    {
        ArgumentNullException.ThrowIfNull(skin);

        var paths = skin.Animations.Values
            .Concat(skin.GazeAnimations.Values)
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
    /// Gets the decoded frame for a file path.
    /// </summary>
    /// <param name="path">
    /// Frame file path as listed in the skin.
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
    /// Frame file path as listed in the skin.
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
