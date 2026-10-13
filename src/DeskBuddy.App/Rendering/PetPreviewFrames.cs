using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Decodes the pictures shown in the pet previews, one at a time and only when a preview needs them. Unlike
/// <see cref="PetFrameCache"/> it does not decode every picture of a pet or build hit-test masks, as the panel may show
/// many pets at once.
/// </summary>
public sealed class PetPreviewFrames
{
    /// <summary>
    /// The pictures decoded so far by file path; <see langword="null"/> for a file that could not be decoded.
    /// </summary>
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the picture in a file, decoding it the first time.
    /// </summary>
    /// <param name="path">
    /// The file path of the picture.
    /// </param>
    /// <returns>
    /// The frozen picture, or <see langword="null"/> if the file cannot be read as an image.
    /// </returns>
    public ImageSource? Get(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var image = Decode(path);
        _cache[path] = image;
        return image;
    }

    /// <summary>
    /// Decodes a picture.
    /// </summary>
    /// <param name="path">
    /// The file path of the picture.
    /// </param>
    /// <returns>
    /// The frozen picture, or <see langword="null"/> if the file cannot be read as an image.
    /// </returns>
    private static BitmapImage? Decode(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FormatException or InvalidOperationException)
        {
            // A broken picture only leaves a gap in the preview; the pet itself reports such a file when it is chosen.
            return null;
        }
    }
}
