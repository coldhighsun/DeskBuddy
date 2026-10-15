using DeskBuddy.Domain.Gaze;

namespace DeskBuddy.Domain.Pets;

/// <summary>
/// Tracks playback position of the current animation of a <see cref="Pet"/>. Pure logic with no UI dependency.
/// </summary>
public sealed class AnimationPlayer
{
    /// <summary>
    /// The pet whose animations are played.
    /// </summary>
    private readonly Pet _pet;

    /// <summary>
    /// Time accumulated on the current frame.
    /// </summary>
    private TimeSpan _elapsedOnFrame;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationPlayer"/> class playing <see cref="PetState.Idle"/>.
    /// </summary>
    /// <param name="pet">
    /// The pet to play animations from.
    /// </param>
    public AnimationPlayer(Pet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        _pet = pet;
        Animation = pet.GetAnimation(PetState.Idle);
    }

    /// <summary>
    /// Gets the state currently requested.
    /// </summary>
    public PetState State { get; private set; } = PetState.Idle;

    /// <summary>
    /// Gets the animation currently playing, after Idle fallback has been applied.
    /// </summary>
    public PetAnimation Animation { get; private set; }

    /// <summary>
    /// Gets the index of the current frame within <see cref="Animation"/>.
    /// </summary>
    public int FrameIndex { get; private set; }

    /// <summary>
    /// Gets the name of the current frame picture (see <see cref="FrameId"/>).
    /// </summary>
    public string CurrentFrameId => Animation.FrameIds[FrameIndex];

    /// <summary>
    /// Switches to the animation of a state. Playback restarts only if the resolved animation actually changes.
    /// </summary>
    /// <param name="state">
    /// The state to show.
    /// </param>
    /// <param name="gaze">
    /// Where the pet looks while idle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the displayed frame changed; otherwise <see langword="false"/>.
    /// </returns>
    public bool SetState(PetState state, GazeDirection gaze = GazeDirection.None)
    {
        State = state;

        var next = _pet.GetAnimation(state, gaze);
        if (ReferenceEquals(next, Animation))
        {
            return false;
        }

        Animation = next;
        FrameIndex = 0;
        _elapsedOnFrame = TimeSpan.Zero;
        return true;
    }

    /// <summary>
    /// Advances playback by the given elapsed time, looping at the end of the animation.
    /// </summary>
    /// <param name="delta">
    /// Time elapsed since the previous call. Negative values are ignored.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the displayed frame changed; otherwise <see langword="false"/>.
    /// </returns>
    public bool Advance(TimeSpan delta)
    {
        if (delta <= TimeSpan.Zero || Animation.FrameIds.Count <= 1)
        {
            return false;
        }

        _elapsedOnFrame += delta;

        var frameDuration = Animation.FrameDuration;
        var framesToAdvance = (int)(_elapsedOnFrame / frameDuration);
        if (framesToAdvance == 0)
        {
            return false;
        }

        _elapsedOnFrame -= frameDuration * framesToAdvance;

        var newIndex = (FrameIndex + framesToAdvance) % Animation.FrameIds.Count;
        var changed = newIndex != FrameIndex;
        FrameIndex = newIndex;
        return changed;
    }
}
