using System.IO;
using System.Windows;
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
    /// <param name="path">
    /// Image file path.
    /// </param>
    /// <returns>
    /// The frozen bitmap.
    /// </returns>
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
    /// <param name="id">
    /// The name of the picture (see <see cref="FrameId"/>).
    /// </param>
    /// <param name="load">
    /// Supplies the decoded image file; it lets the caller decide how long a decoded atlas is kept.
    /// </param>
    /// <returns>
    /// The frozen picture.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// The area is not inside the image.
    /// </exception>
    public static BitmapSource Cut(string id, Func<string, BitmapSource> load)
    {
        ArgumentNullException.ThrowIfNull(load);

        var (file, area) = FrameId.Parse(id);
        var image = load(file);
        return area is { } rectangle ? Crop(image, rectangle) : image;
    }

    /// <summary>
    /// Cuts an area out of an image into a picture of its own.
    /// </summary>
    /// <param name="image">
    /// The image, normally the atlas of a pet.
    /// </param>
    /// <param name="area">
    /// The area as x, y, width and height in pixels.
    /// </param>
    /// <returns>
    /// The frozen picture.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// The area is not inside the image, which means that the file is not the atlas the manifest describes (it may have been
    /// replaced since the pet was loaded); like the other failures to read a pet, it is one that
    /// <see cref="PetLoadFailure.Is"/> counts as a pet that cannot be used.
    /// </exception>
    public static BitmapSource Crop(BitmapSource image, (int X, int Y, int Width, int Height) area)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (area.X < 0 || area.Y < 0 || area.Width <= 0 || area.Height <= 0
            || area.X + area.Width > image.PixelWidth || area.Y + area.Height > image.PixelHeight)
        {
            throw new InvalidDataException($"The area {area} is not inside the {image.PixelWidth}x{image.PixelHeight} image.");
        }

        // The pixels are copied out: a CroppedBitmap would keep the whole atlas alive for as long as any one frame is used.
        var cropped = new CroppedBitmap(image, new Int32Rect(area.X, area.Y, area.Width, area.Height));
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
