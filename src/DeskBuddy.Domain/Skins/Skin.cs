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
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="animations"/> has no <see cref="PetState.Idle"/> entry.
    /// </exception>
    public Skin(string name, int frameWidth, int frameHeight, IReadOnlyDictionary<PetState, SkinAnimation> animations)
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
    /// Gets the animation for a state, falling back to <see cref="PetState.Idle"/> when the skin does not define it.
    /// </summary>
    /// <param name="state">
    /// The requested state.
    /// </param>
    /// <returns>
    /// The animation for <paramref name="state"/>, or the Idle animation if none is defined.
    /// </returns>
    public SkinAnimation GetAnimation(PetState state)
    {
        return _animations.TryGetValue(state, out var animation) ? animation : _animations[PetState.Idle];
    }
}
