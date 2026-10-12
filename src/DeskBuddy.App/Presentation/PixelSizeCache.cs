namespace DeskBuddy.App.Presentation;

/// <summary>
/// Remembers the size of a window in device pixels, so a step that runs many times a second need not ask the window
/// manager for it each time. A failed read (an empty size) is never remembered, so it is read again on the next step.
/// </summary>
/// <param name="readSize">
/// Reads the size from the window.
/// </param>
public sealed class PixelSizeCache(Func<(int Width, int Height)> readSize)
{
    /// <summary>
    /// The size read last, or <see langword="null"/> when it must be read again.
    /// </summary>
    private (int Width, int Height)? _size;

    /// <summary>
    /// Gets the size, reading it from the window if it is not known.
    /// </summary>
    /// <returns>
    /// The width and the height in device pixels.
    /// </returns>
    public (int Width, int Height) Get()
    {
        if (_size is { } known)
        {
            return known;
        }

        var size = readSize();
        if (size.Width > 0 && size.Height > 0)
        {
            _size = size;
        }

        return size;
    }

    /// <summary>
    /// Forgets the size, as when the window was resized or moved to a monitor with another scale.
    /// </summary>
    public void Invalidate()
    {
        _size = null;
    }
}
