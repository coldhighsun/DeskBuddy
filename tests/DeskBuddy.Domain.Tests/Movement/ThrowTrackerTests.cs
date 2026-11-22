namespace DeskBuddy.Domain.Tests.Movement;

/// <summary>
/// Tests for <see cref="ThrowTracker"/>.
/// </summary>
public class ThrowTrackerTests
{
    /// <summary>
    /// The tuning used by the tests.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// The tracker under test.
    /// </summary>
    private readonly ThrowTracker _tracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThrowTrackerTests"/> class.
    /// </summary>
    public ThrowTrackerTests()
    {
        _tracker = new ThrowTracker(_options);
    }

    /// <summary>
    /// A quick flick at the end of the drag throws the pet in that direction.
    /// </summary>
    [Fact]
    public void GetVelocity_QuickFlick_ReturnsItsSpeed()
    {
        // Arrange: 60 pixels to the right and 30 up in 50 ms.
        _tracker.Add(TimeSpan.FromMilliseconds(0), 100, 300);
        _tracker.Add(TimeSpan.FromMilliseconds(25), 130, 285);
        _tracker.Add(TimeSpan.FromMilliseconds(50), 160, 270);

        // Act
        var (x, y) = _tracker.GetVelocity(TimeSpan.FromMilliseconds(50));

        // Assert
        x.Should().BeApproximately(1200, 0.001);
        y.Should().BeApproximately(-600, 0.001);
    }

    /// <summary>
    /// Only the last moments of the drag count, not the slow start of it.
    /// </summary>
    [Fact]
    public void GetVelocity_SlowDragThenFlick_MeasuresOnlyTheFlick()
    {
        // Arrange
        _tracker.Add(TimeSpan.FromMilliseconds(0), 0, 0);
        _tracker.Add(TimeSpan.FromMilliseconds(500), 10, 0);
        _tracker.Add(TimeSpan.FromMilliseconds(550), 60, 0);
        _tracker.Add(TimeSpan.FromMilliseconds(600), 110, 0);

        // Act
        var (x, _) = _tracker.GetVelocity(TimeSpan.FromMilliseconds(600));

        // Assert
        x.Should().BeApproximately(1000, 0.001);
    }

    /// <summary>
    /// A slow release drops the pet instead of throwing it.
    /// </summary>
    [Fact]
    public void GetVelocity_SlowRelease_ReturnsZero()
    {
        // Arrange: 10 pixels in 50 ms is 200 pixels per second.
        _tracker.Add(TimeSpan.FromMilliseconds(0), 100, 100);
        _tracker.Add(TimeSpan.FromMilliseconds(50), 110, 100);

        // Act
        var velocity = _tracker.GetVelocity(TimeSpan.FromMilliseconds(50));

        // Assert
        velocity.Should().Be((0d, 0d));
    }

    /// <summary>
    /// A pointer held still before letting go drops the pet, however fast it moved before.
    /// </summary>
    [Fact]
    public void GetVelocity_PointerRestedBeforeRelease_ReturnsZero()
    {
        // Arrange
        _tracker.Add(TimeSpan.FromMilliseconds(0), 0, 0);
        _tracker.Add(TimeSpan.FromMilliseconds(50), 100, 0);

        // Act
        var velocity = _tracker.GetVelocity(TimeSpan.FromMilliseconds(400));

        // Assert
        velocity.Should().Be((0d, 0d));
    }

    /// <summary>
    /// A wild fling is limited to the fastest throw, keeping its direction.
    /// </summary>
    [Fact]
    public void GetVelocity_TooFast_IsLimited()
    {
        // Arrange: 300 pixels to the left in 50 ms is 6000 pixels per second.
        _tracker.Add(TimeSpan.FromMilliseconds(0), 500, 100);
        _tracker.Add(TimeSpan.FromMilliseconds(50), 200, 100);

        // Act
        var (x, y) = _tracker.GetVelocity(TimeSpan.FromMilliseconds(50));

        // Assert
        x.Should().BeApproximately(-_options.ThrowMaxSpeed, 0.001);
        y.Should().Be(0);
    }

    /// <summary>
    /// A fast flick straight up is limited to the fastest upward throw, so the pet does not fly across the whole screen.
    /// </summary>
    [Fact]
    public void GetVelocity_FlickedUpTooFast_IsLimitedToTheUpwardMaximum()
    {
        // Arrange: 60 pixels up in 50 ms is 1200 pixels per second, below the overall maximum but above the upward one.
        _tracker.Add(TimeSpan.FromMilliseconds(0), 100, 300);
        _tracker.Add(TimeSpan.FromMilliseconds(50), 100, 240);

        // Act
        var (x, y) = _tracker.GetVelocity(TimeSpan.FromMilliseconds(50));

        // Assert
        x.Should().Be(0);
        y.Should().BeApproximately(-_options.ThrowMaxUpSpeed, 0.001);
    }

    /// <summary>
    /// A fast flick downwards is not held back by the upward limit.
    /// </summary>
    [Fact]
    public void GetVelocity_FlickedDownFast_IsNotLimitedByTheUpwardMaximum()
    {
        // Arrange: 60 pixels down in 50 ms is 1200 pixels per second.
        _tracker.Add(TimeSpan.FromMilliseconds(0), 100, 240);
        _tracker.Add(TimeSpan.FromMilliseconds(50), 100, 300);

        // Act
        var (_, y) = _tracker.GetVelocity(TimeSpan.FromMilliseconds(50));

        // Assert
        y.Should().BeApproximately(1200, 0.001);
    }

    /// <summary>
    /// After a reset the positions of the previous drag no longer count.
    /// </summary>
    [Fact]
    public void Reset_AfterFlick_ReturnsZero()
    {
        // Arrange
        _tracker.Add(TimeSpan.FromMilliseconds(0), 0, 0);
        _tracker.Add(TimeSpan.FromMilliseconds(50), 100, 0);

        // Act
        _tracker.Reset();
        var velocity = _tracker.GetVelocity(TimeSpan.FromMilliseconds(50));

        // Assert
        velocity.Should().Be((0d, 0d));
    }
}
