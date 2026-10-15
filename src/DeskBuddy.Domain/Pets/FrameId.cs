using System.Globalization;

namespace DeskBuddy.Domain.Pets;

/// <summary>
/// The name of one picture of a pet, as <see cref="PetAnimation.FrameIds"/> lists it: the path of the file it is in,
/// and, if the file holds many pictures (a pet's atlas), the area of it that this picture is, written as
/// <c>path#x,y,width,height</c> in pixels. A name that is only a path stands for the whole file.
/// </summary>
public static class FrameId
{
    /// <summary>
    /// The character that separates the path from the area.
    /// </summary>
    private const char Separator = '#';

    /// <summary>
    /// Makes the name of a picture that is an area of a file.
    /// </summary>
    /// <param name="path">
    /// The path of the file.
    /// </param>
    /// <param name="x">
    /// The left edge of the area in pixels.
    /// </param>
    /// <param name="y">
    /// The top edge of the area in pixels.
    /// </param>
    /// <param name="width">
    /// The width of the area in pixels.
    /// </param>
    /// <param name="height">
    /// The height of the area in pixels.
    /// </param>
    /// <returns>
    /// The name.
    /// </returns>
    public static string Create(string path, int x, int y, int width, int height)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return string.Create(CultureInfo.InvariantCulture, $"{path}{Separator}{x},{y},{width},{height}");
    }

    /// <summary>
    /// Splits the name of a picture into the file and the area of it.
    /// </summary>
    /// <param name="id">
    /// The name of the picture.
    /// </param>
    /// <returns>
    /// The path of the file, and the area as x, y, width and height in pixels, or <see langword="null"/> if the name is
    /// only a path and the picture is the whole file. A path that contains the separator is told from an area by what
    /// follows the last one: it must be four whole numbers.
    /// </returns>
    public static (string Path, (int X, int Y, int Width, int Height)? Area) Parse(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var index = id.LastIndexOf(Separator);
        if (index > 0)
        {
            var parts = id[(index + 1)..].Split(',');
            if (parts.Length == 4
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var x)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var y)
                && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var width)
                && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var height))
            {
                return (id[..index], (x, y, width, height));
            }
        }

        return (id, null);
    }
}
