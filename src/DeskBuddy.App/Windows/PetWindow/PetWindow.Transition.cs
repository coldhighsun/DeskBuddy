using System.Windows.Media;
using DeskBuddy.App.Presentation;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Windows;

/// <content>
/// The animation played when another pet is switched to.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// Whether the pet is leaving or arriving. While it is, the pet does not react to the mouse, does not change state and
    /// does not walk, so nothing moves or changes under the animation.
    /// </summary>
    private bool _transitioning;

    /// <summary>
    /// The animation under way, from the start of the pet's leaving until the end of the new pet's arriving;
    /// <see langword="null"/> when there is none.
    /// </summary>
    private EffectSession? _session;

    /// <summary>
    /// Gets a value indicating whether the pet may be changed for another one by itself now: it is on screen, no animation
    /// is under way, the mouse is not on it, and it is in a state a change would not interrupt
    /// (<see cref="AutoSwitchPolicy.CanSwitchIn"/>).
    /// </summary>
    internal bool CanAutoSwitch =>
        IsVisible && !_transitioning && !_pressed && AutoSwitchPolicy.CanSwitchIn(_shownState);

    /// <summary>
    /// What <see cref="CanAutoSwitch"/> was when the window last changed in a way that matters to it. A field that any
    /// thread may read, because the automatic pet change asks from the application's thread and the window's own
    /// properties may be read only from the window's.
    /// </summary>
    private volatile bool _autoSwitchAllowed;

    /// <summary>
    /// Gets a value indicating whether the pet could be changed for another one by itself, as the window last knew. Safe to
    /// read from any thread.
    /// </summary>
    public bool AutoSwitchAllowed => _autoSwitchAllowed;

    /// <summary>
    /// Writes <see cref="CanAutoSwitch"/> where <see cref="AutoSwitchAllowed"/> reads it. Call after anything that changes
    /// it: the state shown, a press, an animation beginning or ending, the pet being shown or hidden.
    /// </summary>
    private void PublishAutoSwitch()
    {
        _autoSwitchAllowed = CanAutoSwitch;
    }

    /// <summary>
    /// Lets the pet on screen leave: it stops where it is and the animation of the user's choice takes it away. The new pet
    /// takes its place in <see cref="SetPet"/>, which also plays the second part of the animation.
    /// </summary>
    /// <param name="setting">The animation the user chose, which may be a random one or none.</param>
    /// <returns>A task that completes when the pet is gone; completed at once if nothing is shown or Windows has animations off.</returns>
    internal async Task PlayLeaveAsync(PetSwitchEffect setting)
    {
        if (!IsVisible || _transitioning || !Motion.AnimationsEnabled)
        {
            return;
        }

        var effect = PetSwitchEffectFactory.Create(PetSwitchEffects.Resolve(setting, Random.Shared));
        if (effect is null)
        {
            return;
        }

        _transitioning = true;
        PublishAutoSwitch();
        BecomeIdle();
        _bubble?.Hide();
        _session = new EffectSession(this, SpriteHost, effect, CurrentEffectLayout, () => SpriteImage.Source, () => FlipTransform.ScaleX < 0);
        try
        {
            await _session.LeaveAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            // The pet is put back as it was, and the switch goes on without the rest of the animation.
            AbandonTransition();
            throw;
        }
    }

    /// <summary>
    /// Puts the pet on the ground in the Idle state, whatever it was doing: a walk or a hop stops, a fall ends with the
    /// umbrella folded, and a pet on the edge of the screen comes down. The pet that is taken away is then standing still,
    /// not caught in the middle of a stride or a climb.
    /// </summary>
    private void BecomeIdle()
    {
        if (_fall is not null)
        {
            EndFall(snapToLanding: true);
        }

        if (IsCrawling)
        {
            _brain.OnCrawlEnded();
            Top = CreateMovementContext().Bounds.MaxY;
        }

        _brain.ResetState();
        ApplyState(_brain.State);
        if (_stateHeld)
        {
            // On the seam between two monitors the change is held back until the pet is off it.
            NudgeOffSeam();
            ApplyState(_brain.State);
        }

        _moveTimer.Stop();
    }

    /// <summary>
    /// Plays the second part of the animation, in which the new pet arrives, or just brings the pet back if there is no
    /// animation to finish. Does not wait: the pet is on screen from the moment this is called.
    /// </summary>
    private async void PlayEnter()
    {
        var session = _session;
        _session = null;
        if (session is null || !IsVisible)
        {
            session?.Close();
            RestorePet();
            EndTransition();
            return;
        }

        try
        {
            await session.EnterAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            // The pet is shown below anyway.
        }
        finally
        {
            session.Close();
            EndTransition();
        }
    }

    /// <summary>
    /// Gives up the animation at once, as when the pet is hidden in the middle of it.
    /// </summary>
    private void AbandonTransition()
    {
        var session = _session;
        _session = null;
        session?.Close();
        RestorePet();
        _transitioning = false;
        PublishAutoSwitch();
    }

    /// <summary>
    /// Shows the pet's own content again, whatever an animation did to it.
    /// </summary>
    private void RestorePet()
    {
        SpriteHost.Clip = null;
        Motion.Settle(SpriteHost);
    }

    /// <summary>
    /// Lets the pet react again, and shows its speech bubble if it has something to say.
    /// </summary>
    private void EndTransition()
    {
        _transitioning = false;
        PublishAutoSwitch();
        OnMessageChanged(this, EventArgs.Empty);
    }

    /// <summary>
    /// Works out where the pet window is, for the animation.
    /// </summary>
    /// <returns>The layout of the animation's window around the pet.</returns>
    private EffectLayout CurrentEffectLayout()
    {
        return EffectLayout.For(
            MonitorHelper.GetWindowBoundsPixels(this),
            MonitorHelper.GetWorkAreaPixels(this),
            VisualTreeHelper.GetDpi(this).DpiScaleX);
    }
}
