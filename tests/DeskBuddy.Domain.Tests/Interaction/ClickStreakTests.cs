using DeskBuddy.Domain.Interaction;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Interaction;

/// <summary>
/// Tests for <see cref="ClickStreak"/>.
/// </summary>
public sealed class ClickStreakTests
{
    /// <summary>
    /// The clock the streak is timed with.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// The streak under test.
    /// </summary>
    private readonly ClickStreak _streak;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickStreakTests"/> class.
    /// </summary>
    public ClickStreakTests()
    {
        _streak = new ClickStreak(_time);
    }

    /// <summary>
    /// The first click is a single poke.
    /// </summary>
    [Fact]
    public void Register_FirstClick_IsOneClick()
    {
        // Act
        var tier = _streak.Register();

        // Assert
        tier.Should().Be(ClickTier.OneClick);
    }

    /// <summary>
    /// Clicks close together build up from repeated to annoyed.
    /// </summary>
    [Fact]
    public void Register_QuickClicks_GrowFromRepeatedToAnnoyed()
    {
        // Act
        var tiers = new List<ClickTier>();
        for (var i = 0; i < ClickStreak.AnnoyedAt; i++)
        {
            tiers.Add(_streak.Register());
            _time.Advance(TimeSpan.FromMilliseconds(300));
        }

        // Assert
        tiers.Should().Equal(ClickTier.OneClick, ClickTier.Repeated, ClickTier.Repeated, ClickTier.Annoyed);
    }

    /// <summary>
    /// A pause longer than the gap starts a new streak.
    /// </summary>
    [Fact]
    public void Register_AfterPause_StartsOver()
    {
        // Arrange
        _streak.Register();
        _streak.Register();
        _time.Advance(ClickStreak.Gap + TimeSpan.FromMilliseconds(1));

        // Act
        var tier = _streak.Register();

        // Assert
        tier.Should().Be(ClickTier.OneClick);
    }

    /// <summary>
    /// The reset forgets the clicks so far.
    /// </summary>
    [Fact]
    public void Reset_ForgetsTheStreak()
    {
        // Arrange
        _streak.Register();
        _streak.Register();

        // Act
        _streak.Reset();

        // Assert
        _streak.Register().Should().Be(ClickTier.OneClick);
    }
}
