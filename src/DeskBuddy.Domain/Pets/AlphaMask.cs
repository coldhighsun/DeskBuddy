namespace DeskBuddy.Domain.Pets;

/// <summary>
/// Per-pixel opacity of a sprite frame, used to decide whether a mouse position hits the pet or passes through.
/// </summary>
public sealed class AlphaMask
{
    /// <summary>
    /// Default alpha value (0-255) at or above which a pixel counts as part of the pet.
    /// </summary>
    public const byte DefaultThreshold = 16;

    /// <summary>
    /// Alpha channel values in row-major order.
    /// </summary>
    private readonly byte[] _alpha;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlphaMask"/> class.
    /// </summary>
    /// <param name="width">
    /// Mask width in pixels.
    /// </param>
    /// <param name="height">
    /// Mask height in pixels.
    /// </param>
    /// <param name="alpha">
    /// Alpha values in row-major order; length must equal <c>width * height</c>.
    /// </param>
    public AlphaMask(int width, int height, byte[] alpha)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(alpha);

        if (alpha.Length != width * height)
        {
            throw new ArgumentException("Alpha length must equal width * height.", nameof(alpha));
        }

        Width = width;
        Height = height;
        _alpha = alpha;
    }

    /// <summary>
    /// Gets the mask width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the mask height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Builds a mask from a 32-bit BGRA pixel buffer.
    /// </summary>
    /// <param name="width">
    /// Image width in pixels.
    /// </param>
    /// <param name="height">
    /// Image height in pixels.
    /// </param>
    /// <param name="pixels">
    /// BGRA pixel data (4 bytes per pixel).
    /// </param>
    /// <param name="stride">
    /// Number of bytes per image row in <paramref name="pixels"/>.
    /// </param>
    /// <returns>
    /// The alpha mask of the image.
    /// </returns>
    public static AlphaMask FromBgra32(int width, int height, byte[] pixels, int stride)
    {
        var alpha = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                alpha[(y * width) + x] = pixels[(y * stride) + (x * 4) + 3];
            }
        }

        return new AlphaMask(width, height, alpha);
    }

    /// <summary>
    /// Determines whether a pixel is opaque enough to count as part of the pet.
    /// </summary>
    /// <param name="x">
    /// Pixel column.
    /// </param>
    /// <param name="y">
    /// Pixel row.
    /// </param>
    /// <param name="threshold">
    /// Minimum alpha value that counts as opaque.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the pixel is inside the mask and its alpha is at least <paramref name="threshold"/>.
    /// </returns>
    public bool IsOpaque(int x, int y, byte threshold = DefaultThreshold)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return false;
        }

        return _alpha[(y * Width) + x] >= threshold;
    }

    /// <summary>
    /// Finds where the sprite starts: the first row that holds an opaque pixel.
    /// </summary>
    /// <param name="threshold">
    /// Minimum alpha value that counts as opaque.
    /// </param>
    /// <returns>
    /// The top of the sprite as a fraction of the frame height, from 0 (the first row) up to but not including 1; 0 when
    /// the frame has no opaque pixel at all.
    /// </returns>
    public double TopOpaqueFraction(byte threshold = DefaultThreshold)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (_alpha[(y * Width) + x] >= threshold)
                {
                    return y / (double)Height;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Determines whether a position given as a fraction of the frame size hits an opaque pixel. Independent of DPI
    /// scaling, because the position is relative to the frame.
    /// </summary>
    /// <param name="u">
    /// Horizontal position from 0 (left edge) to 1 (right edge).
    /// </param>
    /// <param name="v">
    /// Vertical position from 0 (top edge) to 1 (bottom edge).
    /// </param>
    /// <param name="threshold">
    /// Minimum alpha value that counts as opaque.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the position maps to an opaque pixel; positions outside the frame return
    /// <see langword="false"/>.
    /// </returns>
    public bool IsOpaqueAtNormalized(double u, double v, byte threshold = DefaultThreshold)
    {
        if (u < 0 || v < 0 || u >= 1 || v >= 1)
        {
            return false;
        }

        return IsOpaque((int)(u * Width), (int)(v * Height), threshold);
    }
}
