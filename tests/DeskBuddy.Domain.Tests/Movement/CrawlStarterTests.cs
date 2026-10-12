using AwesomeAssertions;
using DeskBuddy.Domain.Movement;

namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="CrawlStarter"/>.
/// </summary>
public class CrawlStarterTests
{
    /// <summary>
    /// The range the pet may take: 1000 wide, 600 high.
    /// </summary>
    private static readonly MovementBounds Bounds = new(0, 1000, 0, 600);

    /// <summary>
    /// The size of the pet window.
    /// </summary>
    private static readonly Point2 Size = new(64, 64);

    /// <summary>
    /// A screen with no other monitor next to it.
    /// </summary>
    private static readonly FakeEdges NoMonitors = new();

    /// <summary>
    /// A pet that walks into the left end climbs it, when the chance comes out in its favor.
    /// </summary>
    [Fact]
    public void AtWalkEnd_LeftEndAndLuck_ClimbsLeftSideUp()
    {
        // Act
        var start = CrawlStarter.AtWalkEnd(new Point2(0, 600), Bounds, 0.3, new FixedRandom(0.1, 0), NoMonitors);

        // Assert
        start.Should().Be(new CrawlStart(CrawlPhase.Wall, OnRightEdge: false, ClimbingUp: true, 0, 600));
    }

    /// <summary>
    /// A pet that walks into the right end climbs the right side.
    /// </summary>
    [Fact]
    public void AtWalkEnd_RightEndAndLuck_ClimbsRightSideUp()
    {
        // Act
        var start = CrawlStarter.AtWalkEnd(new Point2(1000, 600), Bounds, 0.3, new FixedRandom(0.1, 0), NoMonitors);

        // Assert
        start.Should().Be(new CrawlStart(CrawlPhase.Wall, OnRightEdge: true, ClimbingUp: true, 0, 600));
    }

    /// <summary>
    /// A pet in the middle of its line does not climb, whatever the chance.
    /// </summary>
    [Fact]
    public void AtWalkEnd_NotAtEnd_ReturnsNull()
    {
        // Act
        var start = CrawlStarter.AtWalkEnd(new Point2(500, 600), Bounds, 1, new FixedRandom(0, 0), NoMonitors);

        // Assert
        start.Should().BeNull();
    }

    /// <summary>
    /// When the chance does not come out in its favor the pet walks on, and the monitors are not even asked about.
    /// </summary>
    [Fact]
    public void AtWalkEnd_NoLuck_ReturnsNullWithoutAskingForMonitors()
    {
        // Arrange
        var edges = new FakeEdges();

        // Act
        var start = CrawlStarter.AtWalkEnd(new Point2(0, 600), Bounds, 0.3, new FixedRandom(0.9, 0), edges);

        // Assert
        start.Should().BeNull();
        edges.Asked.Should().BeFalse();
    }

    /// <summary>
    /// An end of the line that is only a seam to the next monitor is no wall to climb.
    /// </summary>
    [Fact]
    public void AtWalkEnd_MonitorBeside_ReturnsNull()
    {
        // Act
        var start = CrawlStarter.AtWalkEnd(new Point2(0, 600), Bounds, 1, new FixedRandom(0, 0), new FakeEdges { Left = true });

        // Assert
        start.Should().BeNull();
    }

    /// <summary>
    /// An area that is no wider than the pet has no ends to walk into.
    /// </summary>
    [Fact]
    public void AtWalkEnd_AreaTooNarrow_ReturnsNull()
    {
        // Act
        var start = CrawlStarter.AtWalkEnd(new Point2(0, 600), new MovementBounds(0, 0, 0, 600), 1, new FixedRandom(0, 0), NoMonitors);

        // Assert
        start.Should().BeNull();
    }

    /// <summary>
    /// A pet dropped at the top edge crawls along it, wherever sideways, and the chance picks the first direction.
    /// </summary>
    /// <param name="roll">
    /// The value the chance gives for the direction.
    /// </param>
    /// <param name="expectedDirection">
    /// The expected first direction.
    /// </param>
    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, 1)]
    public void OnDrop_AtTop_StartsOnCeiling(int roll, int expectedDirection)
    {
        // Act
        var start = CrawlStarter.OnDrop(new Point2(400, 5), Size, Bounds, 8, new FixedRandom(0, roll), NoMonitors);

        // Assert
        start.Should().Be(new CrawlStart(CrawlPhase.Ceiling, OnRightEdge: false, ClimbingUp: false, expectedDirection, 0));
    }

    /// <summary>
    /// The top of a monitor with another one above it is no edge of the screen.
    /// </summary>
    [Fact]
    public void OnDrop_AtTopWithMonitorAbove_ReturnsNull()
    {
        // Act
        var start = CrawlStarter.OnDrop(new Point2(400, 5), Size, Bounds, 8, new FixedRandom(0, 0), new FakeEdges { Above = true });

        // Assert
        start.Should().BeNull();
    }

    /// <summary>
    /// A top corner with a monitor above can still be climbed from the side.
    /// </summary>
    [Fact]
    public void OnDrop_TopCornerWithMonitorAbove_ClimbsTheSide()
    {
        // Act
        var start = CrawlStarter.OnDrop(new Point2(2, 5), Size, Bounds, 8, new FixedRandom(0, 0), new FakeEdges { Above = true });

        // Assert
        start.Should().Be(new CrawlStart(CrawlPhase.Wall, OnRightEdge: false, ClimbingUp: true, 0, 5));
    }

    /// <summary>
    /// A pet dropped on the ground at a side can only climb up.
    /// </summary>
    [Fact]
    public void OnDrop_OnGroundAtLeftSide_ClimbsUp()
    {
        // Act: the chance would send it down.
        var start = CrawlStarter.OnDrop(new Point2(3, 598), Size, Bounds, 8, new FixedRandom(0, 1), NoMonitors);

        // Assert
        start.Should().Be(new CrawlStart(CrawlPhase.Wall, OnRightEdge: false, ClimbingUp: true, 0, 598));
    }

    /// <summary>
    /// A pet dropped high up at a side goes up or down as the chance decides.
    /// </summary>
    /// <param name="roll">
    /// The value the chance gives for the direction.
    /// </param>
    /// <param name="expectedUp">
    /// Whether the pet is expected to climb up.
    /// </param>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void OnDrop_HighAtRightSide_GoesAsChanceDecides(int roll, bool expectedUp)
    {
        // Act
        var start = CrawlStarter.OnDrop(new Point2(997, 300), Size, Bounds, 8, new FixedRandom(0, roll), NoMonitors);

        // Assert
        start.Should().Be(new CrawlStart(CrawlPhase.Wall, OnRightEdge: true, ClimbingUp: expectedUp, 0, 300));
    }

    /// <summary>
    /// A pet dropped away from every edge does not crawl.
    /// </summary>
    [Fact]
    public void OnDrop_AwayFromEdges_ReturnsNull()
    {
        // Act
        var start = CrawlStarter.OnDrop(new Point2(500, 300), Size, Bounds, 8, new FixedRandom(0, 0), NoMonitors);

        // Assert
        start.Should().BeNull();
    }

    /// <summary>
    /// A side that only is a seam to the next monitor is no wall to climb.
    /// </summary>
    [Fact]
    public void OnDrop_MonitorBesideLeft_ReturnsNull()
    {
        // Act
        var start = CrawlStarter.OnDrop(new Point2(2, 300), Size, Bounds, 8, new FixedRandom(0, 0), new FakeEdges { Left = true });

        // Assert
        start.Should().BeNull();
    }

    /// <summary>
    /// A screen whose edges are real edges unless it is told that a monitor lies there.
    /// </summary>
    private sealed class FakeEdges : IScreenEdges
    {
        /// <summary>
        /// Gets or sets a value indicating whether a monitor lies beside the left edge.
        /// </summary>
        public bool Left { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether a monitor lies above the top edge.
        /// </summary>
        public bool Above { get; init; }

        /// <summary>
        /// Gets a value indicating whether anything was asked.
        /// </summary>
        public bool Asked { get; private set; }

        /// <inheritdoc />
        public bool HasMonitorBeside(bool left)
        {
            Asked = true;
            return left && Left;
        }

        /// <inheritdoc />
        public bool HasMonitorAbove()
        {
            Asked = true;
            return Above;
        }
    }

    /// <summary>
    /// A random source that always gives the same values.
    /// </summary>
    /// <param name="chance">
    /// The value of <see cref="NextDouble"/>.
    /// </param>
    /// <param name="next">
    /// The value of <see cref="Next(int)"/>.
    /// </param>
    private sealed class FixedRandom(double chance, int next) : Random
    {
        /// <inheritdoc />
        public override double NextDouble()
        {
            return chance;
        }

        /// <inheritdoc />
        public override int Next(int maxValue)
        {
            return next;
        }
    }
}
