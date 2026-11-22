using System.Windows;
using System.Windows.Media;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Windows;

/// <summary>
/// One pet switch's animation from start to end: the window it is drawn in and the animation, which is played in two parts
/// with the pet replaced in between. Looks at the pet window through functions, so it can be told where the pet is and what
/// it looks like at the time each part starts.
/// </summary>
internal sealed class EffectSession
{
    /// <summary>
    /// The pet window the animation is for.
    /// </summary>
    private readonly Window _petWindow;

    /// <summary>
    /// The pet's own content, which the animation may fade or clip.
    /// </summary>
    private readonly UIElement _pet;

    /// <summary>
    /// The animation.
    /// </summary>
    private readonly IPetSwitchEffect _effect;

    /// <summary>
    /// Tells where the pet window is now.
    /// </summary>
    private readonly Func<EffectLayout> _layout;

    /// <summary>
    /// Tells the picture the pet shows now.
    /// </summary>
    private readonly Func<ImageSource?> _frame;

    /// <summary>
    /// Tells whether the pet's picture is shown mirrored left to right now.
    /// </summary>
    private readonly Func<bool> _mirrored;

    /// <summary>
    /// The window the animation is drawn in; <see langword="null"/> before it starts and after it was closed.
    /// </summary>
    private EffectWindow? _window;

    /// <summary>
    /// What the animation draws on, while the window is open.
    /// </summary>
    private EffectStage? _stage;

    /// <summary>
    /// Initializes a new instance of the <see cref="EffectSession"/> class.
    /// </summary>
    /// <param name="petWindow">The pet window the animation is for.</param>
    /// <param name="pet">The pet's own content, which the animation may fade or clip.</param>
    /// <param name="effect">The animation.</param>
    /// <param name="layout">Tells where the pet window is now.</param>
    /// <param name="frame">Tells the picture the pet shows now.</param>
    /// <param name="mirrored">Tells whether the pet's picture is shown mirrored left to right now.</param>
    public EffectSession(
        Window petWindow,
        UIElement pet,
        IPetSwitchEffect effect,
        Func<EffectLayout> layout,
        Func<ImageSource?> frame,
        Func<bool> mirrored)
    {
        _petWindow = petWindow;
        _pet = pet;
        _effect = effect;
        _layout = layout;
        _frame = frame;
        _mirrored = mirrored;
    }

    /// <summary>
    /// Plays the part in which the pet on screen leaves.
    /// </summary>
    /// <returns>A task that completes when the pet is gone.</returns>
    public async Task LeaveAsync()
    {
        _window = new EffectWindow();
        _stage = new EffectStage(_window, _petWindow, _pet, _layout(), _frame(), _mirrored());
        await _effect.LeaveAsync(_stage);
    }

    /// <summary>
    /// Plays the part in which the new pet arrives. The pet window must already show the new pet.
    /// </summary>
    /// <returns>A task that completes when the pet has arrived.</returns>
    public async Task EnterAsync()
    {
        if (_stage is null)
        {
            return;
        }

        _stage.Relayout(_layout(), _frame(), _mirrored());
        await _effect.EnterAsync(_stage);
    }

    /// <summary>
    /// Closes the window of the animation and gives the pet its own content back, whatever state the animation left it in.
    /// </summary>
    public void Close()
    {
        _pet.Clip = null;
        Motion.Settle(_pet);

        var window = _window;
        _window = null;
        _stage = null;
        window?.Close();
    }
}
