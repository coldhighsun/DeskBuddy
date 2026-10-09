namespace DeskBuddy.Domain.Skins;

/// <summary>
/// Tracks playback position of the current animation of a <see cref="Skin"/>. Pure logic with no UI dependency.
/// </summary>
public sealed class AnimationPlayer
{
    /// <summary>
    /// The skin whose animations are played.
    /// </summary>
    private readonly Skin _skin;

    /// <summary>
    /// Time accumulated on the current frame.
    /// </summary>
    private TimeSpan _elapsedOnFrame;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationPlayer"/> class playing <see cref="PetState.Idle"/>.
    /// </summary>
    /// <param name="skin">
    /// The skin to play animations from.
    /// </param>
    public AnimationPlayer(Skin skin)
    {
        ArgumentNullException.ThrowIfNull(skin);

        _skin = skin;
        Animation = skin.GetAnimation(PetState.Idle);
    }

    /// <summary>
    /// Gets the state currently requested.
    /// </summary>
    public PetState State { get; private set; } = PetState.Idle;

    /// <summary>
    /// Gets the animation currently playing, after Idle fallback has been applied.
    /// </summary>
    public SkinAnimation Animation { get; private set; }

    /// <summary>
    /// Gets the index of the current frame within <see cref="Animation"/>.
    /// </summary>
    public int FrameIndex { get; private set; }

    /// <summary>
    /// Gets the file path of the current frame image.
    /// </summary>
    public string CurrentFramePath => Animation.FramePaths[FrameIndex];

    /// <summary>
    /// Switches to the animation of a state. Playback restarts only if the resolved animation actually changes.
    /// </summary>
    /// <param name="state">
    /// The state to show.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the displayed frame changed; otherwise <see langword="false"/>.
    /// </returns>
    public bool SetState(PetState state)
    {
        State = state;

        var next = _skin.GetAnimation(state);
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
        if (delta <= TimeSpan.Zero || Animation.FramePaths.Count <= 1)
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

        var newIndex = (FrameIndex + framesToAdvance) % Animation.FramePaths.Count;
        var changed = newIndex != FrameIndex;
        FrameIndex = newIndex;
        return changed;
    }
}
