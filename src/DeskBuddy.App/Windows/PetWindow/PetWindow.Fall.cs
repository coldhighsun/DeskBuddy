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
/// Falling after a drag and landing.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// The motion of the fall in progress, or <see langword="null"/> when the pet is not falling.
    /// </summary>
    private FallState? _fall;

    /// <summary>
    /// Lets a pet that was dropped well above where it would rest fall down to it.
    /// </summary>
    /// <param name="refreshWorkArea">
    /// Whether to work out the range the pet may take afresh first; <see langword="false"/> when that has just been done,
    /// as in a movement tick.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the pet now falls; the timer then moves it until it lands.
    /// </returns>
    private bool TryStartFall(bool refreshWorkArea = true)
    {
        if (refreshWorkArea)
        {
            RefreshWorkArea();
        }

        // The pet lands on the taskbar line, which is the bottom of the work area.
        var bounds = CreateMovementContext().Bounds;
        var y = CurrentPosition().Y;
        if (!FreeFall.ShouldFall(y, bounds.MaxY, _options.FallMinHeight))
        {
            return false;
        }

        Left = bounds.Clamp(CurrentPosition()).X;
        NudgeOffSeam();
        _fall = new FallState(y, 0, false, false);
        _brain.OnFallStarted();
        return true;
    }

    /// <summary>
    /// Moves the falling pet one step, opens the umbrella or parachute once it is fast enough, and folds it away when
    /// the pet lands.
    /// </summary>
    /// <param name="elapsed">
    /// The time since the previous step.
    /// </param>
    private void OnFallTick(TimeSpan elapsed)
    {
        if (_fall is not { } current)
        {
            return;
        }

        RefreshWorkAreaPeriodically();

        // Worked out afresh on every step, so a change of pet size or work area in mid-air still lands on the ground.
        var bounds = CreateMovementContext().Bounds;
        var next = FreeFall.Step(current, bounds.MaxY, elapsed, _options);
        _fall = next;
        _umbrella.MovePet(Math.Clamp(CurrentPosition().X, bounds.MinX, Math.Max(bounds.MinX, bounds.MaxX)), next.Y);

        if (next.Opened && !current.Opened)
        {
            _umbrella.Open();

            // The pet takes hold of the handle as the canopy opens.
            _brain.OnFallOpened();
            ApplyState(_brain.State);
        }

        if (next.Landed)
        {
            _brain.OnFallEnded();
            ApplyState(_brain.State);
        }
    }

    /// <summary>
    /// Ends the fall in progress: the umbrella or parachute folds away.
    /// </summary>
    /// <param name="snapToLanding">
    /// Whether the pet is put on the ground at once; <see langword="false"/> leaves it where it is, as when the user
    /// grabs it in mid-air.
    /// </param>
    private void EndFall(bool snapToLanding)
    {
        if (snapToLanding && _fall is not null)
        {
            Top = CreateMovementContext().Bounds.MaxY;
        }

        _fall = null;
        _umbrella.Fold();
    }

    /// <summary>
    /// Puts the pet where it rests, on the monitor it is on: on the taskbar line.
    /// </summary>
    private void SettleInWorkArea()
    {
        RefreshWorkArea();

        // A pet in mid-air keeps falling to the ground (see OnFallTick); only keep it inside the work area sideways.
        if (_fall is not null)
        {
            Left = CreateMovementContext().Bounds.Clamp(CurrentPosition()).X;
            return;
        }

        // A pet crawling along the edges stays where it is, inside the work area; the crawl puts it right again.
        if (IsCrawling)
        {
            var kept = CreateMovementContext().Bounds.Clamp(CurrentPosition());
            Left = kept.X;
            Top = kept.Y;
            return;
        }

        var rest = _movement.Settle(CurrentPosition(), CreateMovementContext().Bounds);
        Left = rest.X;
        Top = rest.Y;
        NudgeOffSeam();
    }
}
