using DeskBuddy.Domain.Chatter;

namespace DeskBuddy.Domain.Tests.Chatter;

/// <summary>
/// Tests for <see cref="ChatterPolicy"/>.
/// </summary>
public class ChatterPolicyTests
{
    /// <summary>
    /// The longest wait before a remark: the interval plus the full jitter.
    /// </summary>
    private static readonly TimeSpan LongestWait = ChatterPolicy.Interval + TimeSpan.FromMinutes(5);

    /// <summary>
    /// The fake clock shared with the policy.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The policy under test, enabled.
    /// </summary>
    private readonly ChatterPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatterPolicyTests"/> class.
    /// </summary>
    public ChatterPolicyTests()
    {
        _policy = new ChatterPolicy(_clock, new Random(3)) { IsEnabled = true };
    }

    /// <summary>
    /// The pet does not speak the moment it is first asked.
    /// </summary>
    [Fact]
    public void Update_FirstCall_StaysQuiet()
    {
        // Act
        var remark = _policy.Update(canSpeak: true);

        // Assert
        remark.Should().BeNull();
    }

    /// <summary>
    /// Before the minimum interval has passed there is never a remark.
    /// </summary>
    [Fact]
    public void Update_BeforeInterval_StaysQuiet()
    {
        // Arrange
        _policy.Update(true);

        // Act
        _clock.Advance(ChatterPolicy.Interval - TimeSpan.FromSeconds(1));
        var remark = _policy.Update(true);

        // Assert
        remark.Should().BeNull();
    }

    /// <summary>
    /// After the longest wait a remark is certainly due, and it is a valid index.
    /// </summary>
    [Fact]
    public void Update_AfterLongestWait_ReturnsValidRemark()
    {
        // Arrange
        _policy.Update(true);

        // Act
        _clock.Advance(LongestWait);
        var remark = _policy.Update(true);

        // Assert
        remark.Should().NotBeNull();
        remark!.Value.Should().BeInRange(0, ChatterPolicy.VariantCount - 1);
    }

    /// <summary>
    /// After a remark the next one needs a full interval again.
    /// </summary>
    [Fact]
    public void Update_AfterRemark_WaitsAFullIntervalAgain()
    {
        // Arrange
        _policy.Update(true);
        _clock.Advance(LongestWait);
        _policy.Update(true);

        // Act
        _clock.Advance(ChatterPolicy.Interval - TimeSpan.FromSeconds(1));
        var tooSoon = _policy.Update(true);
        _clock.Advance(LongestWait);
        var later = _policy.Update(true);

        // Assert
        tooSoon.Should().BeNull();
        later.Should().NotBeNull();
    }

    /// <summary>
    /// A remark that comes due while the pet may not speak is said as soon as it may.
    /// </summary>
    [Fact]
    public void Update_DueWhileCannotSpeak_SpeaksWhenItCan()
    {
        // Arrange
        _policy.Update(true);
        _clock.Advance(LongestWait);

        // Act
        var held = _policy.Update(canSpeak: false);
        var spoken = _policy.Update(canSpeak: true);

        // Assert
        held.Should().BeNull();
        spoken.Should().NotBeNull();
    }

    /// <summary>
    /// A disabled policy never speaks, however long it waits.
    /// </summary>
    [Fact]
    public void Update_Disabled_NeverSpeaks()
    {
        // Arrange
        _policy.IsEnabled = false;

        // Act
        _clock.Advance(TimeSpan.FromHours(5));
        var remark = _policy.Update(true);

        // Assert
        remark.Should().BeNull();
    }

    /// <summary>
    /// Switching the policy off and on again restarts the wait instead of speaking at once.
    /// </summary>
    [Fact]
    public void Update_ReEnabled_WaitsAgain()
    {
        // Arrange
        _policy.Update(true);
        _policy.IsEnabled = false;
        _policy.Update(true);
        _clock.Advance(TimeSpan.FromHours(5));
        _policy.IsEnabled = true;

        // Act
        var remark = _policy.Update(true);

        // Assert
        remark.Should().BeNull();
    }

    /// <summary>
    /// The same remark is never said twice in a row, and every remark gets used over time.
    /// </summary>
    [Fact]
    public void Update_ManyRemarks_NeverRepeatsBackToBack()
    {
        // Arrange
        _policy.Update(true);
        var said = new List<int>();

        // Act
        for (var i = 0; i < 200; i++)
        {
            _clock.Advance(LongestWait);
            said.Add(_policy.Update(true)!.Value);
        }

        // Assert
        said.Zip(said.Skip(1)).Should().OnlyContain(pair => pair.First != pair.Second);
        said.Distinct().Should().HaveCount(ChatterPolicy.VariantCount);
    }
}
