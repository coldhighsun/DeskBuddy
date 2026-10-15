using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskBuddy.App.Rendering;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Gaze;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Windows;

/// <content>
/// Pet state: applying the brain's decisions, facing, animation and visibility.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// Re-evaluates the pet's state right away and shows it. Call after anything that can change the state, such as a
    /// user command, instead of waiting for the next timer tick.
    /// </summary>
    private void RefreshState()
    {
        ApplyState(_brain.Tick());
    }

    /// <summary>
    /// Switches the displayed state: swaps the animation, and starts or stops the walking timer.
    /// </summary>
    /// <param name="state">
    /// The state to show.
    /// </param>
    private void ApplyState(PetState state)
    {
        if (state == _shownState)
        {
            return;
        }

        // A pet on the seam between two monitors walks on until it is on one of them; whatever else the brain wants is
        // shown then, as soon as the movement tick sees it off the seam. Being picked up or falling is not held back.
        if (SeamGuard.HoldsStateChange(_shownState, state, IsOnSeam()))
        {
            _stateHeld = true;
            return;
        }

        _stateHeld = false;

        // A crawl that ends high above the ground (an alert, sleep, letting go, switching climbing off) ends in a fall under
        // the umbrella, not in a jump to the ground. Being picked up is not an end; the drop decides.
        if (_shownState.EndsCrawlInFall(state) && TryStartFall())
        {
            ApplyState(_brain.State);
            return;
        }

        var previousState = _shownState;
        _shownState = state;

        // Grabbed again or otherwise interrupted in mid-air: the umbrella folds, and the fall is over.
        if (previousState.IsFalling() && !state.IsFalling())
        {
            EndFall(snapToLanding: false);
        }

        // Interrupted while crawling along the edges (an alert, sleep, hiding): the pet goes back to the ground. Being
        // picked up is not an interruption; the drop decides.
        if (previousState.IsCrawl() && !state.IsCrawl())
        {
            if (state != PetState.Drag)
            {
                _facing = 1;
                ApplyFacing();
                SettleInWorkArea();
            }
        }

        // Letting go of the edge: the pet falls back to where it rests, on the taskbar line. Being picked up from the
        // edge is not letting go; the drop decides.
        if (_hanging && state != PetState.Hang)
        {
            _hanging = false;
            if (state != PetState.Drag)
            {
                _facing = 1;
                ApplyFacing();
                SettleInWorkArea();
            }
        }

        // The scene of a pet at its computer, and the scenes that carry a written label, are drawn facing right and
        // read wrongly mirrored. Walking sets the facing again, so nothing else needs to undo this. Hanging on to the
        // right edge needs the mirrored picture, and has no label.
        if (_facing != 1 && (state == PetState.Coding || (ScenarioCatalog.All.Contains(state) && state != PetState.Hang)))
        {
            _facing = 1;
            ApplyFacing();
        }

        // What the pet is doing, said in words when the mouse rests on it; nothing for plain states.
        SpriteImage.ToolTip = Strings.StateHint(state);

        if (_player.SetState(state, CurrentLook()))
        {
            ShowFrame();
        }

        _animationWatch.Restart();
        _animationTimer.Interval = _player.Animation.FrameDuration;

        if (previousState == PetState.Hop && !state.IsWalking())
        {
            LandFromHop();
        }

        if (state == PetState.Hop)
        {
            // A hop is part of the walk in progress: same direction, no new walk.
            _hopWatch.Restart();
            StartMoveTimerIfVisible();
        }
        else if (state == PetState.Walk)
        {
            if (previousState != PetState.Hop)
            {
                BeginWalk();
            }

            StartMoveTimerIfVisible();
        }
        else if (state.IsFalling() || state.IsCrawl())
        {
            StartMoveTimerIfVisible();
        }
        else
        {
            _moveTimer.Stop();
        }
    }

    /// <summary>
    /// Starts the walking timer, unless the pet is hidden; showing the pet again restarts it.
    /// </summary>
    private void StartMoveTimerIfVisible()
    {
        if (IsVisible)
        {
            _moveWatch.Restart();
            _moveTimer.Start();
        }
    }

    /// <summary>
    /// Shows or hides the window to match the brain's visibility. While hidden the animation and walking timers are
    /// stopped, so a hidden pet costs next to nothing; the brain timer keeps running because the
    /// check for the full-screen application going away depends on it.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        if (_brain.Visibility.IsVisible)
        {
            if (!IsVisible)
            {
                Show();
                _animationWatch.Restart();
                _animationTimer.Start();
                UpdatePointerTimer();
                if (_shownState.IsWalking() || _shownState.IsFalling() || _shownState.IsCrawl())
                {
                    StartMoveTimerIfVisible();
                }
            }
        }
        else if (IsVisible)
        {
            _animationTimer.Stop();
            _moveTimer.Stop();
            _pointerTimer.Stop();

            // A pet that is hidden mid-air lands at once, so it is not stuck falling while nobody sees it.
            if (_fall is not null)
            {
                EndFall(snapToLanding: true);
                _brain.OnFallEnded();
                ApplyState(_brain.State);
            }

            // A pet that is hidden while crawling goes back to the ground.
            if (IsCrawling)
            {
                _brain.OnCrawlEnded();
                Top = CreateMovementContext().Bounds.MaxY;
                ApplyState(_brain.State);
            }

            Hide();
        }
    }

    /// <summary>
    /// Mirrors the sprite so it faces the walking direction.
    /// </summary>
    private void ApplyFacing()
    {
        FlipTransform.ScaleX = _facing < 0 ? -1 : 1;
    }

    /// <summary>
    /// Advances the animation by the real elapsed time and swaps the sprite when the frame changes.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var delta = _animationWatch.Elapsed;
        _animationWatch.Restart();

        if (_player.Advance(delta))
        {
            ShowFrame();
        }
    }

    /// <summary>
    /// Lets the brain re-evaluate the pet's state and shows the result.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnBrainTick(object? sender, EventArgs e)
    {
        RefreshState();
        UpdatePointerTimer();
    }
}
