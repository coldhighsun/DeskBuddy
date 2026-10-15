using System.Globalization;
using DeskBuddy.Domain.Gaze;

namespace DeskBuddy.Domain.Pets;

/// <summary>
/// A loaded pet pack: a named set of animations keyed by pet state.
/// </summary>
public sealed class Pet
{
    /// <summary>
    /// Animations keyed by the state they represent.
    /// </summary>
    private readonly IReadOnlyDictionary<PetState, PetAnimation> _animations;

    /// <summary>
    /// The name of the pet in other languages, by culture name or language.
    /// </summary>
    private readonly Dictionary<string, string> _localizedNames;

    /// <summary>
    /// The Idle animations for looking in a direction, if the pet has any.
    /// </summary>
    private readonly IReadOnlyDictionary<GazeDirection, PetAnimation> _gazeAnimations;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pet"/> class.
    /// </summary>
    /// <param name="name">
    /// Display name of the pet.
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
    /// Variants of the Idle animation for looking left, right, up or down, or <see langword="null"/> if the pet has
    /// none.
    /// </param>
    /// <param name="localizedNames">
    /// The name of the pet in other languages, keyed by culture name (<c>zh-CN</c>) or language (<c>zh</c>), or
    /// <see langword="null"/> if it has none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="animations"/> has no <see cref="PetState.Idle"/> entry.
    /// </exception>
    public Pet(
        string name,
        int frameWidth,
        int frameHeight,
        IReadOnlyDictionary<PetState, PetAnimation> animations,
        IReadOnlyDictionary<GazeDirection, PetAnimation>? gazeAnimations = null,
        IReadOnlyDictionary<string, string>? localizedNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameHeight);
        ArgumentNullException.ThrowIfNull(animations);

        if (!animations.ContainsKey(PetState.Idle))
        {
            throw new ArgumentException("A pet must define an Idle animation.", nameof(animations));
        }

        Name = name;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        _animations = animations;
        _gazeAnimations = gazeAnimations ?? new Dictionary<GazeDirection, PetAnimation>();
        _localizedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in localizedNames ?? new Dictionary<string, string>())
        {
            // Keys that differ only by case or surrounding blanks name the same language; the last one wins, as a
            // cosmetic duplicate in a manifest must not make the pet unusable.
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                _localizedNames[key.Trim()] = value.Trim();
            }
        }
    }

    /// <summary>
    /// Gets the display name of the pet.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name of the pet to show in a language: the one the manifest gives for the culture, else for its parent
    /// cultures (<c>zh-CN</c>, then <c>zh-Hans</c>, then <c>zh</c>), else <see cref="Name"/>.
    /// </summary>
    /// <param name="culture">
    /// The language to show the name in.
    /// </param>
    /// <returns>
    /// The name.
    /// </returns>
    public string GetDisplayName(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        for (var current = culture; current.Name.Length > 0; current = current.Parent)
        {
            if (_localizedNames.TryGetValue(current.Name, out var name))
            {
                return name;
            }
        }

        return Name;
    }

    /// <summary>
    /// Gets the frame width in device-independent pixels.
    /// </summary>
    public int FrameWidth { get; }

    /// <summary>
    /// Gets the frame height in device-independent pixels.
    /// </summary>
    public int FrameHeight { get; }

    /// <summary>
    /// Gets all animations defined by this pet.
    /// </summary>
    public IReadOnlyDictionary<PetState, PetAnimation> Animations => _animations;

    /// <summary>
    /// Gets the Idle animations for looking in a direction.
    /// </summary>
    public IReadOnlyDictionary<GazeDirection, PetAnimation> GazeAnimations => _gazeAnimations;

    /// <summary>
    /// Gets a value indicating whether the pet can look in directions, so the pet may follow the mouse pointer.
    /// </summary>
    public bool CanLookAround => _gazeAnimations.Count > 0;

    /// <summary>
    /// Gets how long one hop of the pet lasts, which is one run through its own Hop animation, or
    /// <see langword="null"/> if the pet has none and so does not hop.
    /// </summary>
    public TimeSpan? HopDuration => _animations.TryGetValue(PetState.Hop, out var hop) ? hop.Duration : null;

    /// <summary>
    /// Gets the animation for a state. When the pet does not define it, the closest state it does define is used (see
    /// <see cref="PetStateFallback"/>), and in the end Idle.
    /// </summary>
    /// <param name="state">
    /// The requested state.
    /// </param>
    /// <param name="gaze">
    /// Where the pet looks; only matters for <see cref="PetState.Idle"/>, and only if the pet has a variant for it.
    /// </param>
    /// <returns>
    /// The animation for <paramref name="state"/>, or for the nearest fallback the pet defines, or the Idle animation.
    /// </returns>
    public PetAnimation GetAnimation(PetState state, GazeDirection gaze = GazeDirection.None)
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
