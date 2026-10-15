using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskBuddy.App;

/// <summary>
/// Loads the application icon that is embedded in the assembly (the same file as the executable's icon).
/// </summary>
internal static partial class AppIcon
{
    /// <summary>
    /// The pack URI of the embedded icon. <c>PanelWindow.xaml</c> uses the same address, and the csproj embeds the file.
    /// </summary>
    public const string PackUri = "pack://application:,,,/Assets/DeskBuddy.ico";

    /// <summary>
    /// The alpha value above which a pixel counts as part of the pet.
    /// </summary>
    private const int MinimumVisibleAlpha = 16;

    /// <summary>
    /// The share of the pet's larger side that the icon shows.
    /// </summary>
    private const double FocusShare = 0.75;

    /// <summary>
    /// <c>GetSystemMetrics</c> index of the width of a small icon, the size the notification area draws.
    /// </summary>
    private const int SmallIconWidthMetric = 49;

    /// <summary>
    /// <c>GetSystemMetrics</c> index of the height of a small icon.
    /// </summary>
    private const int SmallIconHeightMetric = 50;

    /// <summary>
    /// Loads the icon at the system's small-icon size, so the notification area shows the matching frame of the file
    /// instead of scaling a larger one down.
    /// </summary>
    /// <returns>
    /// The icon; the caller disposes it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The icon is not embedded in the assembly.
    /// </exception>
    public static Icon LoadSmall()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri(PackUri))
            ?? throw new InvalidOperationException($"The application icon {PackUri} is not embedded in the assembly.");
        using var stream = resource.Stream;
        return new Icon(stream, new System.Drawing.Size(GetSystemMetrics(SmallIconWidthMetric), GetSystemMetrics(SmallIconHeightMetric)));
    }

    /// <summary>
    /// Loads the picture of a pet as an icon at the system's small-icon size, for the notification area.
    /// </summary>
    /// <param name="picture">
    /// The picture of the pet.
    /// </param>
    /// <returns>
    /// The icon; the caller disposes it.
    /// </returns>
    public static Icon FromPicture(ImageSource picture)
    {
        return FromPicture(picture, GetSystemMetrics(SmallIconWidthMetric), GetSystemMetrics(SmallIconHeightMetric));
    }

    /// <summary>
    /// Makes an icon of a pet's picture: only the upper part of the pet is used (see <see cref="FocusArea"/>), so that it
    /// fills the icon and stays recognizable at 16 pixels; that part is scaled to fit in the given size, keeping its
    /// proportions and centered, and stored as the one PNG image of an icon file that is read back as an icon.
    /// </summary>
    /// <param name="picture">
    /// The picture of the pet.
    /// </param>
    /// <param name="width">
    /// The width of the icon in pixels.
    /// </param>
    /// <param name="height">
    /// The height of the icon in pixels.
    /// </param>
    /// <returns>
    /// The icon; the caller disposes it.
    /// </returns>
    public static Icon FromPicture(ImageSource picture, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        if (picture is BitmapSource bitmap)
        {
            var area = FocusArea(bitmap);
            if (!area.IsEmpty)
            {
                picture = new CroppedBitmap(bitmap, area);
            }
        }

        return Render(picture, width, height);
    }

    /// <summary>
    /// Loads a picture made to be the icon of a pet (its head, in the pet's <c>icon.png</c>) as an icon at the system's
    /// small-icon size, as it is, without choosing a part of it.
    /// </summary>
    /// <param name="picture">
    /// The icon picture of the pet.
    /// </param>
    /// <returns>
    /// The icon; the caller disposes it.
    /// </returns>
    public static Icon FromIconPicture(ImageSource picture)
    {
        ArgumentNullException.ThrowIfNull(picture);

        return Render(picture, GetSystemMetrics(SmallIconWidthMetric), GetSystemMetrics(SmallIconHeightMetric));
    }

    /// <summary>
    /// Makes an icon of a picture, scaled to fit in the given size, keeping its proportions and centered, and stored as
    /// the one PNG image of an icon file that is read back as an icon.
    /// </summary>
    /// <param name="picture">
    /// The picture.
    /// </param>
    /// <param name="width">
    /// The width of the icon in pixels.
    /// </param>
    /// <param name="height">
    /// The height of the icon in pixels.
    /// </param>
    /// <returns>
    /// The icon; the caller disposes it.
    /// </returns>
    private static Icon Render(ImageSource picture, int width, int height)
    {
        var scale = Math.Min(width / picture.Width, height / picture.Height);
        var drawn = new Rect((width - (picture.Width * scale)) / 2, (height - (picture.Height * scale)) / 2, picture.Width * scale, picture.Height * scale);
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(picture, drawn);
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var png = new MemoryStream();
        encoder.Save(png);

        // An icon file with a single image: the directory (6 bytes), one entry (16 bytes), then the PNG. A size of 256 or
        // more is written as 0.
        using var file = new MemoryStream();
        using (var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((byte)(width >= 256 ? 0 : width));
            writer.Write((byte)(height >= 256 ? 0 : height));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((int)png.Length);
            writer.Write(22);
            writer.Write(png.GetBuffer(), 0, (int)png.Length);
        }

        file.Position = 0;
        return new Icon(file, new System.Drawing.Size(width, height));
    }

    /// <summary>
    /// Works out which part of a pet's picture the icon shows: the pet is found as the box around the pixels that are not
    /// transparent, and the icon gets the top three quarters of it (the head and the upper body, whatever the pet is), as
    /// wide as the pet, but no wider than three quarters of the larger side of that box, around its middle.
    /// </summary>
    /// <param name="bitmap">
    /// The picture of the pet.
    /// </param>
    /// <returns>
    /// The area in pixels, or an empty one if the picture has nothing that is not transparent.
    /// </returns>
    internal static Int32Rect FocusArea(BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var converted = bitmap.Format == PixelFormats.Bgra32 ? bitmap : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var y = 0; y < converted.PixelHeight; y++)
        {
            for (var x = 0; x < converted.PixelWidth; x++)
            {
                if (pixels[(y * stride) + (x * 4) + 3] > MinimumVisibleAlpha)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        if (right < 0)
        {
            return Int32Rect.Empty;
        }

        var boxWidth = right - left + 1;
        var boxHeight = bottom - top + 1;
        var side = Math.Max(1, (int)Math.Round(Math.Max(boxWidth, boxHeight) * FocusShare));
        var width = Math.Min(boxWidth, side);
        var height = Math.Min(boxHeight, side);
        return new Int32Rect(left + ((boxWidth - width) / 2), top, width, height);
    }

    /// <summary>
    /// Reads a system metric.
    /// </summary>
    /// <param name="index">
    /// The metric to read.
    /// </param>
    /// <returns>
    /// The value of the metric.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}
