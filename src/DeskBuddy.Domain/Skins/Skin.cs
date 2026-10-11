using DeskBuddy.Domain.Gaze;

namespace DeskBuddy.Domain.Skins;

/// <summary>
/// A loaded skin pack: a named set of animations keyed by pet state.
/// </summary>
public sealed class Skin
{
    /// <summary>
    /// Animations keyed by the state they represent.
    /// </summary>
    private readonly IReadOnlyDictionary<PetState, SkinAnimation> _animations;

    /// <summary>
    /// The Idle animations for looking in a direction, if the skin has any.
    /// </summary>
    private readonly IReadOnlyDictionary<GazeDirection, SkinAnimation> _gazeAnimations;

    /// <summary>
    /// Initializes a new instance of the <see cref="Skin"/> class.
    /// </summary>
    /// <param name="name">
    /// Display name of the skin.
    /// </param>
    /// <param name="frameWidth">
    /// Frame width in device-independent pixels.
    /// </param>
    /// <param name="frameHeight">
    /// Frame height in device-independent pixels.
    /// </param>
    /// <param name="animations">
    /// Animations keyed by state. Must contain <see cref="PetState.Idle"/>, which is the fallback for missing states.
    /// </param>
    /// <param name="gazeAnimations">
    /// Variants of the Idle animation for looking left, right, up or down, or <see langword="null"/> if the skin has
    /// none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="animations"/> has no <see cref="PetState.Idle"/> entry.
    /// </exception>
    public Skin(
        string name,
        int frameWidth,
        int frameHeight,
        IReadOnlyDictionary<PetState, SkinAnimation> animations,
        IReadOnlyDictionary<GazeDirection, SkinAnimation>? gazeAnimations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameHeight);
        ArgumentNullException.ThrowIfNull(animations);

        if (!animations.ContainsKey(PetState.Idle))
        {
            throw new ArgumentException("A skin must define an Idle animation.", nameof(animations));
        }

        Name = name;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        _animations = animations;
        _gazeAnimations = gazeAnimations ?? new Dictionary<GazeDirection, SkinAnimation>();
    }

    /// <summary>
    /// Gets the display name of the skin.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the frame width in device-independent pixels.
    /// </summary>
    public int FrameWidth { get; }

    /// <summary>
    /// Gets the frame height in device-independent pixels.
    /// </summary>
    public int FrameHeight { get; }

    /// <summary>
    /// Gets all animations defined by this skin.
    /// </summary>
    public IReadOnlyDictionary<PetState, SkinAnimation> Animations => _animations;

    /// <summary>
    /// Gets the Idle animations for looking in a direction.
    /// </summary>
    public IReadOnlyDictionary<GazeDirection, SkinAnimation> GazeAnimations => _gazeAnimations;

    /// <summary>
    /// Gets a value indicating whether the skin can look in directions, so the pet may follow the mouse pointer.
    /// </summary>
    public bool CanLookAround => _gazeAnimations.Count > 0;

    /// <summary>
    /// Gets the animation for a state. When the skin does not define it, the closest state it does define is used (see
    /// <see cref="PetStateFallback"/>), and in the end Idle.
    /// </summary>
    /// <param name="state">
    /// The requested state.
    /// </param>
    /// <param name="gaze">
    /// Where the pet looks; only matters for <see cref="PetState.Idle"/>, and only if the skin has a variant for it.
    /// </param>
    /// <returns>
    /// The animation for <paramref name="state"/>, or for the nearest fallback the skin defines, or the Idle animation.
    /// </returns>
    public SkinAnimation GetAnimation(PetState state, GazeDirection gaze = GazeDirection.None)
    {
        if (state == PetState.Idle && _gazeAnimations.TryGetValue(gaze, out var looking))
        {
            return looking;
        }

        for (PetState? candidate = state; candidate is { } current; candidate = PetStateFallback.Of(current))
        {
            if (_animations.TryGetValue(current, out var animation))
            {
                return animation;
            }
        }

        return _animations[PetState.Idle];
    }
}
