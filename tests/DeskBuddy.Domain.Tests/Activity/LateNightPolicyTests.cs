namespace DeskBuddy.Domain.Tests.Activity;

/// <summary>
/// Tests for <see cref="LateNightPolicy"/>.
/// </summary>
public class LateNightPolicyTests
{
    /// <summary>
    /// A session long enough to count.
    /// </summary>
    private static readonly TimeSpan LongSession = TimeSpan.FromHours(3);

    /// <summary>
    /// The fake clock; its local time zone is UTC, so local and UTC time agree.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly LateNightPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="LateNightPolicyTests"/> class.
    /// </summary>
    public LateNightPolicyTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _policy = new LateNightPolicy(new PetBehaviorOptions(), _clock);
    }

    /// <summary>
    /// Sets the time of day.
    /// </summary>
    /// <param name="hour">
    /// The hour, 0 to 23.
    /// </param>
    private void SetHour(int hour)
    {
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 11, hour, 30, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// A long session late at night counts, before midnight as well as after.
    /// </summary>
    /// <param name="hour">
    /// The hour of the night.
    /// </param>
    [Theory]
    [InlineData(23)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public void IsLateNightSession_LongSessionAtNight_IsTrue(int hour)
    {
        // Arrange
        SetHour(hour);

        // Act
        var result = _policy.IsLateNightSession(LongSession);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// A long session in the daytime or evening does not count.
    /// </summary>
    /// <param name="hour">
    /// The hour of the day.
    /// </param>
    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(22)]
    public void IsLateNightSession_LongSessionByDay_IsFalse(int hour)
    {
        // Arrange
        SetHour(hour);

        // Act
        var result = _policy.IsLateNightSession(LongSession);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// A short session does not count, even at night.
    /// </summary>
    [Fact]
    public void IsLateNightSession_ShortSessionAtNight_IsFalse()
    {
        // Arrange
        SetHour(1);

        // Act
        var result = _policy.IsLateNightSession(TimeSpan.FromMinutes(30));

        // Assert
        result.Should().BeFalse();
    }
}
