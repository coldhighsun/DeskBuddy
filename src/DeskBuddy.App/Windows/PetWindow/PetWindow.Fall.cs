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
    /// The sideways speed of a thrown pet in mid-air, in device-independent pixels per second; zero for a plain drop.
    /// </summary>
    private double _throwVelocityX;

    /// <summary>
    /// Whether the fall in progress moved the pet sideways, so it may have landed across two monitors.
    /// </summary>
    private bool _thrownSideways;

    /// <summary>
    /// Lets a pet that was dropped well above where it would rest fall down to it.
    /// </summary>
    /// <param name="refreshWorkArea">
    /// Whether to work out the range the pet may take afresh first; <see langword="false"/> when that has just been done,
    /// as in a movement tick.
    /// </param>
    /// <param name="throwVelocity">
    /// The speed the pet was thrown with, in device-independent pixels per second, or zero for a plain drop. A pet
    /// thrown upwards flies even from close to the ground.
    /// </param>
    /// <returns><see langword="true"/> if the pet now falls; the timer then moves it until it lands.</returns>
    private bool TryStartFall(bool refreshWorkArea = true, (double X, double Y) throwVelocity = default)
    {
        if (refreshWorkArea)
        {
            RefreshWorkArea();
        }

        // The pet lands on the taskbar line, which is the bottom of the work area.
        var bounds = CreateMovementContext().Bounds;
        var y = CurrentPosition().Y;
        var thrownUp = throwVelocity.Y < 0;
        if (!FreeFall.ShouldFall(y, bounds.MaxY, _options.FallMinHeight) && !thrownUp)
        {
            return false;
        }

        Left = bounds.Clamp(CurrentPosition()).X;
        _throwVelocityX = throwVelocity.X;
        _thrownSideways = throwVelocity.X != 0;
        if (!_thrownSideways)
        {
            NudgeOffSeam();
        }

        _fall = new FallState(y, throwVelocity.Y, false, false);
        _brain.OnFallStarted();
        return true;
    }

    /// <summary>
    /// Moves the falling pet one step, opens the umbrella or parachute once it is fast enough, and folds it away when
    /// the pet lands.
    /// </summary>
    /// <param name="elapsed">The time since the previous step.</param>
    private void OnFallTick(TimeSpan elapsed)
    {
        if (_fall is not { } current)
        {
            return;
        }

        RefreshWorkAreaPeriodically();

        // Worked out afresh on every step, so a change of pet size or work area in mid-air still lands on the ground.
        var bounds = CreateMovementContext().Bounds;
        var x = Math.Clamp(CurrentPosition().X, bounds.MinX, Math.Max(bounds.MinX, bounds.MaxX));
        if (_throwVelocityX != 0)
        {
            var sideways = ThrowMotion.Step(new ThrowState(x, _throwVelocityX), bounds.MinX, bounds.MaxX, elapsed, _options);
            x = sideways.X;
            _throwVelocityX = sideways.Velocity;
        }

        var next = FreeFall.Step(current, bounds.MaxY, elapsed, _options, bounds.MinY);
        _fall = next;
        _umbrella.MovePet(x, next.Y);

        if (next.Opened && !current.Opened)
        {
            // A pet that flies down on its own wings gets no canopy; it just slows down and spreads its wings.
            if (!_pet.FliesDown)
            {
                _umbrella.Open(_pet.GlideGrips, mirrored: _facing < 0);
            }

            // The pet takes hold of the handle as the canopy opens (or starts to fly).
            _brain.OnFallOpened();
            ApplyState(_brain.State);
        }

        if (next.Landed)
        {
            _throwVelocityX = 0;
            if (_thrownSideways)
            {
                _thrownSideways = false;
                NudgeOffSeam();
            }

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
        _throwVelocityX = 0;
        _thrownSideways = false;
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
