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
/// Crawling and hanging on screen edges.
/// </content>
public partial class PetWindow
{
    /// <summary>
    /// Gets a value indicating whether the pet is crawling along the edges of the screen.
    /// </summary>
    private bool IsCrawling => _shownState.IsCrawl();

    /// <summary>
    /// Gets a value indicating whether the pet may crawl along the edges of the screen now: the user allows it and the
    /// pet has the pictures for it.
    /// </summary>
    private bool CanCrawl =>
        _climbEdges
        && _pet.Animations.ContainsKey(PetState.Climb)
        && _pet.Animations.ContainsKey(PetState.Ceiling);

    /// <summary>
    /// Lets a pet that has just walked into the left or right end of the screen start to climb it, by chance.
    /// </summary>
    /// <param name="position">
    /// Where the walk step put the pet.
    /// </param>
    /// <param name="bounds">
    /// The range the pet may take.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the pet now climbs; the state then needs showing.
    /// </returns>
    private bool TryStartCrawlAtEnd(Point2 position, MovementBounds bounds)
    {
        if (!CanCrawl || _dragging)
        {
            return false;
        }

        var start = CrawlStarter.AtWalkEnd(
            position,
            bounds,
            _options.ClimbChance,
            Random.Shared,
            this);
        if (start is not { } crawlStart)
        {
            return false;
        }

        StartCrawl(crawlStart);
        return true;
    }

    /// <summary>
    /// Starts to crawl along an edge of the screen.
    /// </summary>
    /// <param name="start">
    /// Where and how the pet starts.
    /// </param>
    private void StartCrawl(CrawlStart start)
    {
        LandFromHop();
        RefreshWorkArea();
        _moveTicksSinceWorkAreaRefresh = 0;
        var bounds = CreateMovementContext().Bounds;

        if (start.Phase == CrawlPhase.Ceiling)
        {
            _facing = _crawl.StartOnCeiling(start.Direction);
            Left = bounds.Clamp(CurrentPosition()).X;
            Top = bounds.MinY;
            NudgeOffSeam();
            _brain.OnCrawlStarted(PetState.Ceiling);
        }
        else
        {
            _facing = _crawl.StartOnWall(start.OnRightEdge, start.ClimbingUp);
            Left = start.OnRightEdge ? Math.Max(bounds.MinX, bounds.MaxX) : bounds.MinX;
            Top = start.Y;
            _brain.OnCrawlStarted(_crawl.CrawlState);
        }

        ApplyFacing();
    }

    /// <summary>
    /// Moves the crawling pet one step, and ends the crawl when it reaches the ground or lets go.
    /// </summary>
    /// <param name="delta">
    /// The time since the previous step.
    /// </param>
    private void OnCrawlTick(TimeSpan delta)
    {
        if (!IsCrawling)
        {
            return;
        }

        RefreshWorkAreaPeriodically();

        var current = CurrentPosition();
        var step = _crawl.Step(current, CreateMovementContext().Bounds, delta, holdCourse: IsOnSeam(), edges: this);

        // The top edge of monitors side by side can be at different heights; the pet changes height quickly rather than
        // jumping when it crawls from one to the other.
        var position = step.Position;
        if (_rowAreas.Count > 1
            && step.Phase == CrawlPhase.Ceiling
            && VerticalEase.Classify(current.Y, position.Y, _options.VerticalEaseTolerance) != VerticalChange.None)
        {
            position = position with
            {
                Y = VerticalEase.Approach(current.Y, position.Y, _options.VerticalEaseSpeed * delta.TotalSeconds),
            };
        }

        MoveToStepPosition(position);

        if (step.Outcome != CrawlOutcome.Continue)
        {
            EndCrawl();
            return;
        }

        if (step.Facing != _facing)
        {
            _facing = step.Facing;
            ApplyFacing();
        }

        var state = _crawl.CrawlState;
        if (state != _shownState)
        {
            RefreshWorkArea();
            _brain.OnCrawlStarted(state);
            ApplyState(_brain.State);
        }
    }

    /// <summary>
    /// Ends the crawl along the edges of the screen. Showing the new state puts the pet back on the ground, with a fall
    /// under the umbrella if it is high enough above it.
    /// </summary>
    private void EndCrawl()
    {
        _brain.OnCrawlEnded();
        ApplyState(_brain.State);
    }

    /// <summary>
    /// Lets a pet that was dropped at the top or at a side of the work area crawl along that edge, if it may.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the pet now crawls along an edge.
    /// </returns>
    private bool TryCrawlOnEdge()
    {
        if (!CanCrawl)
        {
            return false;
        }

        RefreshWorkArea();
        var start = CrawlStarter.OnDrop(
            CurrentPosition(),
            new Point2(Width, Height),
            CreateMovementContext().Bounds,
            EdgeSnapDistance,
            Random.Shared,
            this);
        if (start is not { } crawlStart)
        {
            return false;
        }

        StartCrawl(crawlStart);
        return true;
    }

    /// <summary>
    /// Lets the pet cling to the left or right edge of the work area if it was dropped close to it, and the pet has a
    /// way of showing that.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the pet now hangs on to an edge.
    /// </returns>
    private bool TryHangOnEdge()
    {
        if (!_pet.Animations.ContainsKey(PetState.Hang) || !_switches.Allows(PetState.Hang))
        {
            return false;
        }

        RefreshWorkArea();
        var bounds = CreateMovementContext().Bounds;
        var snapped = EdgeSnap.Find(
            CurrentPosition(),
            new Point2(Width, Height),
            bounds,
            EdgeSnapDistance,
            allowLeft: !MonitorHelper.HasMonitorBeside(this, left: true),
            allowRight: !MonitorHelper.HasMonitorBeside(this, left: false));
        if (snapped is not { } snap)
        {
            return false;
        }

        Left = snap.Position.X;
        Top = snap.Position.Y;
        _facing = snap.Facing;
        ApplyFacing();
        _hanging = true;
        _brain.ActOut(PetState.Hang, HangDuration);
        return true;
    }
}
