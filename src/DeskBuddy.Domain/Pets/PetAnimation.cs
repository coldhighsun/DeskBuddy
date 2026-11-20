namespace DeskBuddy.Domain.Pets;

/// <summary>
/// A frame-by-frame animation for a single <see cref="PetState"/>.
/// </summary>
/// <param name="Fps">
/// Playback speed in frames per second. Must be greater than zero.
/// </param>
/// <param name="FrameIds">
/// The names of the frame pictures (see <see cref="FrameId"/>: the atlas file and the area of it), in playback order.
/// They are names, not paths of files that can be opened. Must not be empty.
/// </param>
public sealed record PetAnimation(double Fps, IReadOnlyList<string> FrameIds)
{
    /// <summary>
    /// The slowest playback speed a pet may declare, in frames per second. Anything slower would make a frame last longer than
    /// a timer can wait.
    /// </summary>
    public const double MinFps = 0.1;

    /// <summary>
    /// The fastest playback speed a pet may declare, in frames per second. Anything faster cannot be shown and would keep the
    /// animation timer firing without a pause.
    /// </summary>
    public const double MaxFps = 120;

    /// <summary>
    /// Gets the time each frame stays on screen.
    /// </summary>
    public TimeSpan FrameDuration => TimeSpan.FromSeconds(1 / Fps);

    /// <summary>
    /// Gets the time one run through all the frames takes.
    /// </summary>
    public TimeSpan Duration => FrameDuration * FrameIds.Count;
}
