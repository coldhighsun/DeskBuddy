using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One pet in the pets tab: what it is called, what it can do, and a preview that plays the states it defines one after
/// the other. A pet that cannot be loaded is still shown, with the reason.
/// </summary>
public sealed partial class PetCardViewModel : ObservableObject
{
    /// <summary>
    /// How long the preview stays on one state before it shows the next.
    /// </summary>
    public static readonly TimeSpan StateDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Supplies the picture in a file.
    /// </summary>
    private readonly Func<string, ImageSource?>? _frames;

    /// <summary>
    /// Plays the animation of the state being shown; <see langword="null"/> if the pet could not be loaded.
    /// </summary>
    private readonly AnimationPlayer? _player;

    /// <summary>
    /// The states the preview goes through.
    /// </summary>
    private readonly IReadOnlyList<PetState> _showcase = [];

    /// <summary>
    /// The source of randomness for the order the states are shown in.
    /// </summary>
    private readonly Random _random = Random.Shared;

    /// <summary>
    /// The order the states are being shown in: Idle first after a start, then the others at random, and a new random order
    /// each time all have been shown.
    /// </summary>
    private IReadOnlyList<PetState> _order = [];

    /// <summary>
    /// The position in <see cref="_order"/> of the state being shown.
    /// </summary>
    private int _showcaseIndex;

    /// <summary>
    /// How long the preview has been on the current state.
    /// </summary>
    private TimeSpan _timeOnState;

    /// <summary>
    /// Gets or sets a value indicating whether this is the pet in use.
    /// </summary>
    [ObservableProperty]
    private bool _isCurrent;

    /// <summary>
    /// Gets or sets a value indicating whether the user picked this pet to look at. Only the picked pet's preview moves;
    /// choosing it for use is a separate step.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets or sets the picture shown in the preview.
    /// </summary>
    [ObservableProperty]
    private ImageSource? _currentFrame;

    /// <summary>
    /// Gets or sets the name of the state the preview is showing.
    /// </summary>
    [ObservableProperty]
    private string _stateCaption = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetCardViewModel"/> class for a pet that was loaded.
    /// </summary>
    /// <param name="folderName">
    /// The name of the pet's folder, which is what the settings store.
    /// </param>
    /// <param name="pet">
    /// The loaded pet.
    /// </param>
    /// <param name="frames">
    /// Supplies the picture in a file.
    /// </param>
    /// <param name="random">
    /// The source of randomness for the order the states are shown in, or <see langword="null"/> to use the shared one.
    /// </param>
    public PetCardViewModel(string folderName, Pet pet, Func<string, ImageSource?> frames, Random? random = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(folderName);
        ArgumentNullException.ThrowIfNull(pet);
        ArgumentNullException.ThrowIfNull(frames);

        FolderName = folderName;
        DisplayName = pet.GetDisplayName(CultureInfo.CurrentUICulture);
        _frames = frames;
        _player = new AnimationPlayer(pet);
        _showcase = PetShowcase.NativeStatesOf(pet);
        _random = random ?? Random.Shared;
        _order = PetShowcase.RandomOrder(_showcase, _random, first: PetState.Idle);

        var capabilities = PetCapabilities.Of(pet);
        InfoText = string.Format(
            CultureInfo.CurrentCulture,
            Strings.PetsInfo,
            pet.FrameWidth,
            pet.FrameHeight,
            capabilities.NativeStateCount,
            capabilities.TotalStateCount);
        Tags =
        [
            new PetTag(Strings.PetsCapLookAround, capabilities.CanLookAround),
            new PetTag(Format(Strings.PetsCapEdges, capabilities.Edges), capabilities.Edges.IsSupported),
            new PetTag(Format(Strings.PetsCapDevelopment, capabilities.Development), capabilities.Development.IsSupported),
            new PetTag(Format(Strings.PetsCapScenarios, capabilities.Scenarios), capabilities.Scenarios.IsSupported),
        ];

        Details = string.Join(
            Environment.NewLine,
            [DisplayName, InfoText, .. Tags.Select(tag => $"{(tag.IsSupported ? "✓" : "✗")} {tag.Text}")]);

        ShowState();
        HasPicture = CurrentFrame is not null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PetCardViewModel"/> class for a pet that could not be loaded.
    /// </summary>
    /// <param name="folderName">
    /// The name of the pet's folder.
    /// </param>
    /// <param name="loadError">
    /// Why the pet could not be loaded.
    /// </param>
    public PetCardViewModel(string folderName, string loadError)
    {
        ArgumentException.ThrowIfNullOrEmpty(folderName);
        ArgumentException.ThrowIfNullOrEmpty(loadError);

        FolderName = folderName;
        DisplayName = folderName;
        LoadError = string.Format(CultureInfo.CurrentCulture, Strings.PetsLoadFailed, loadError);
    }

    /// <summary>
    /// Gets the name of the pet's folder, which is what the settings store.
    /// </summary>
    public string FolderName { get; }

    /// <summary>
    /// Gets the name the pet gives itself in its manifest, or the folder name if it could not be loaded.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the picture size and the number of states, as a line of text.
    /// </summary>
    public string InfoText { get; } = string.Empty;

    /// <summary>
    /// Gets what the pet can do, for comparing pets at a glance.
    /// </summary>
    public IReadOnlyList<PetTag> Tags { get; } = [];

    /// <summary>
    /// Gets the text of the tooltip of the card: the name, the picture size and number of states, and then every tag on a
    /// line of its own, marked with a check or a cross; <see langword="null"/> if the pet could not be loaded, so that no
    /// tooltip is shown.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Gets a value indicating whether the first picture of the preview could be read. A pet that was loaded but whose
    /// pictures could not be read (a corrupt or locked atlas) has none; the tab loads such a card again when it looks at the
    /// pets, as the atlas may have been repaired since.
    /// </summary>
    public bool HasPicture { get; }

    /// <summary>
    /// Gets why the pet could not be loaded, or empty if it could.
    /// </summary>
    public string LoadError { get; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the pet was loaded and can be chosen.
    /// </summary>
    public bool IsAvailable => _player is not null;

    /// <summary>
    /// Gets the name of the picture the preview shows now (see <see cref="FrameId"/>); <see langword="null"/> if the pet could not be loaded.
    /// </summary>
    public string? CurrentFrameId => _player?.CurrentFrameId;

    /// <summary>
    /// Gets the state the preview is showing.
    /// </summary>
    public PetState? CurrentState => _player is null ? null : _order[_showcaseIndex];

    /// <summary>
    /// Moves the preview on: plays the animation, and after <see cref="StateDuration"/> goes to the next state.
    /// </summary>
    /// <param name="delta">
    /// The time since the previous call.
    /// </param>
    public void Tick(TimeSpan delta)
    {
        if (_player is null || delta <= TimeSpan.Zero)
        {
            return;
        }

        _timeOnState += delta;
        if (_timeOnState >= StateDuration && _showcase.Count > 1)
        {
            _timeOnState = TimeSpan.Zero;
            _showcaseIndex++;
            if (_showcaseIndex >= _order.Count)
            {
                // Everything has been shown: a new random order, which does not start with the state just shown.
                _order = PetShowcase.RandomOrder(_showcase, _random, notFirst: _order[^1]);
                _showcaseIndex = 0;
            }

            ShowState();
            return;
        }

        if (_player.Advance(delta))
        {
            UpdateFrame();
        }
    }

    /// <summary>
    /// Starts the preview over from Idle, followed by the other states in a new random order.
    /// </summary>
    public void Restart()
    {
        if (_player is null)
        {
            return;
        }

        _timeOnState = TimeSpan.Zero;
        _order = PetShowcase.RandomOrder(_showcase, _random, first: PetState.Idle);
        _showcaseIndex = 0;
        ShowState();
    }

    /// <summary>
    /// Writes a group of states as text such as <c>Climbing 4/4</c>.
    /// </summary>
    /// <param name="format">
    /// The text with the name of the group and the placeholders for the counts.
    /// </param>
    /// <param name="coverage">
    /// How many states of the group the pet defines.
    /// </param>
    /// <returns>
    /// The text.
    /// </returns>
    private static string Format(string format, StateCoverage coverage)
    {
        return string.Format(CultureInfo.CurrentCulture, format, coverage.Supported, coverage.Total);
    }

    /// <summary>
    /// Switches the preview to the current showcase state.
    /// </summary>
    private void ShowState()
    {
        if (_player is null)
        {
            return;
        }

        var state = _order[_showcaseIndex];
        _player.SetState(state);
        StateCaption = Strings.PetStateName(state);
        UpdateFrame();
    }

    /// <summary>
    /// Shows the picture the animation is on.
    /// </summary>
    private void UpdateFrame()
    {
        if (_player is not null && _frames is not null)
        {
            CurrentFrame = _frames(_player.CurrentFrameId);
        }
    }
}

/// <summary>
/// One ability of a pet shown as a small label, dimmed when the pet does not have it.
/// </summary>
/// <param name="Text">
/// The label.
/// </param>
/// <param name="IsSupported">
/// Whether the pet has the ability.
/// </param>
public sealed record PetTag(string Text, bool IsSupported);
