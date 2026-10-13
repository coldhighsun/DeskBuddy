namespace DeskBuddy.Domain.Pets;

/// <summary>
/// A frame-by-frame animation for a single <see cref="PetState"/>.
/// </summary>
/// <param name="Fps">
/// Playback speed in frames per second. Must be greater than zero.
/// </param>
/// <param name="FramePaths">
/// Absolute paths of the frame images, in playback order. Must not be empty.
/// </param>
public sealed record PetAnimation(double Fps, IReadOnlyList<string> FramePaths)
{
    /// <summary>
    /// Gets the time each frame stays on screen.
    /// </summary>
    public TimeSpan FrameDuration => TimeSpan.FromSeconds(1 / Fps);
}
