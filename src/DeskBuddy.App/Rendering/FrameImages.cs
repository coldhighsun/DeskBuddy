using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Rendering;

/// <summary>
/// Reads the pictures of a pet out of its atlas: decodes an image file and cuts a picture, named as
/// <see cref="FrameId"/> names it, out of it. Shared by <see cref="PetFrameCache"/> and <see cref="PetPreviewFrames"/>,
/// which differ in how long they keep the decoded atlas and in what they do about a picture that cannot be read.
/// </summary>
internal static class FrameImages
{
    /// <summary>
    /// Decodes an image file into a frozen bitmap, fully loaded so the file handle is released.
    /// </summary>
    /// <param name="path">Image file path.</param>
    /// <returns>The frozen bitmap.</returns>
    public static BitmapSource Load(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Gets the picture a name stands for: the whole image file, or the area of it that the name gives.
    /// </summary>
    /// <param name="id">The name of the picture (see <see cref="FrameId"/>).</param>
    /// <param name="load">
    /// Supplies the decoded image file; it lets the caller decide how long a decoded atlas is kept.
    /// </param>
    /// <param name="maxEdge">
    /// The longest edge in pixels the picture may have, or <see langword="null"/> to keep the size of the area; see
    /// <see cref="Crop"/>.
    /// </param>
    /// <returns>The frozen picture.</returns>
    /// <exception cref="InvalidDataException">
    /// The area is not inside the image.
    /// </exception>
    public static BitmapSource Cut(string id, Func<string, BitmapSource> load, int? maxEdge = null)
    {
        ArgumentNullException.ThrowIfNull(load);

        var (file, area) = FrameId.Parse(id);
        var image = load(file);
        return area is { } rectangle ? Crop(image, rectangle, maxEdge) : image;
    }

    /// <summary>
    /// Cuts an area out of an image into a picture of its own.
    /// </summary>
    /// <param name="image">The image, normally the atlas of a pet.</param>
    /// <param name="area">The area as x, y, width and height in pixels.</param>
    /// <param name="maxEdge">
    /// The longest edge in pixels the picture may have: a larger area is scaled down to it, keeping its proportions; a
    /// smaller one is never scaled up. <see langword="null"/> keeps the size of the area.
    /// </param>
    /// <returns>The frozen picture.</returns>
    /// <exception cref="InvalidDataException">
    /// The area is not inside the image, which means that the file is not the atlas the manifest describes (it may have been
    /// replaced since the pet was loaded); like the other failures to read a pet, it is one that
    /// <see cref="PetLoadFailure.Is"/> counts as a pet that cannot be used.
    /// </exception>
    public static BitmapSource Crop(BitmapSource image, (int X, int Y, int Width, int Height) area, int? maxEdge = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (maxEdge is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEdge), maxEdge, "The longest edge must be positive.");
        }

        if (area.X < 0 || area.Y < 0 || area.Width <= 0 || area.Height <= 0
            || area.X + area.Width > image.PixelWidth || area.Y + area.Height > image.PixelHeight)
        {
            throw new InvalidDataException($"The area {area} is not inside the {image.PixelWidth}x{image.PixelHeight} image.");
        }

        // The pixels are copied out: a CroppedBitmap would keep the whole atlas alive for as long as any one frame is used.
        var cropped = (BitmapSource)new CroppedBitmap(image, new Int32Rect(area.X, area.Y, area.Width, area.Height));
        if (maxEdge is { } limit && Math.Max(area.Width, area.Height) > limit)
        {
            // Scaled in premultiplied alpha, which is also what WPF draws with, so that transparent edges do not darken.
            var scale = limit / (double)Math.Max(area.Width, area.Height);
            cropped = new TransformedBitmap(
                new FormatConvertedBitmap(cropped, PixelFormats.Pbgra32, null, 0),
                new ScaleTransform(scale, scale));
        }

        var bytesPerPixel = (cropped.Format.BitsPerPixel + 7) / 8;
        var stride = cropped.PixelWidth * bytesPerPixel;
        var pixels = new byte[stride * cropped.PixelHeight];
        cropped.CopyPixels(pixels, stride, 0);
        var copy = BitmapSource.Create(
            cropped.PixelWidth,
            cropped.PixelHeight,
            image.DpiX,
            image.DpiY,
            cropped.Format,
            cropped.Palette,
            pixels,
            stride);
        copy.Freeze();
        return copy;
    }
}
