using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Schedule;

namespace DeskBuddy.Domain.Tests.Schedule;

/// <summary>
/// Tests for <see cref="ScheduleClock"/>.
/// </summary>
public class ScheduleClockTests
{
    /// <summary>
    /// The options with the working day ending at 18:00.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// The scenario switches; the chime starts off, as it does in the settings.
    /// </summary>
    private readonly ScenarioSwitches _switches = new() { Disabled = [nameof(PetState.Chime)] };

    /// <summary>
    /// The fake time source, running in the UTC zone so UTC and local time agree.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// The object under test.
    /// </summary>
    private readonly ScheduleClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleClockTests"/> class.
    /// </summary>
    public ScheduleClockTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _time.SetUtcNow(At(2026, 3, 11, 9, 0));
        _clock = new ScheduleClock(_options, _time, _switches);
    }

    /// <summary>
    /// An ordinary working hour has nothing going on.
    /// </summary>
    [Fact]
    public void Update_OrdinaryMorning_ReturnsNull()
    {
        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().BeNull();
    }

    /// <summary>
    /// Lunch time comes once, even if asked again and again.
    /// </summary>
    [Fact]
    public void Update_LunchTime_ReturnsLunchOnce()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 11, 45));

        // Act
        var first = _clock.Update();
        _time.Advance(TimeSpan.FromMinutes(5));
        var second = _clock.Update();

        // Assert
        first.Should().Be(new ScheduleOccasion(PetState.Meal, ScheduleClock.Lunch));
        second.Should().BeNull();
    }

    /// <summary>
    /// Lunch is not missed because the user only turns up late in the lunch hour.
    /// </summary>
    [Fact]
    public void Update_FirstCallLateInLunchHour_ReturnsLunch()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 12, 50));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().Be(new ScheduleOccasion(PetState.Meal, ScheduleClock.Lunch));
    }

    /// <summary>
    /// After the lunch hour it is too late for lunch.
    /// </summary>
    [Fact]
    public void Update_AfterLunchHour_ReturnsNull()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 13, 5));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().BeNull();
    }

    /// <summary>
    /// The evening meal is its own occasion, on the same day as lunch.
    /// </summary>
    [Fact]
    public void Update_DinnerTimeAfterLunch_ReturnsDinner()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 12, 0));
        _clock.Update();
        _time.SetUtcNow(At(2026, 3, 11, 17, 40));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().Be(new ScheduleOccasion(PetState.Meal, ScheduleClock.Dinner));
    }

    /// <summary>
    /// The working day ends at the configured time.
    /// </summary>
    [Fact]
    public void Update_ClockOutTime_ReturnsClockOutWeekday()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 18, 1));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().Be(new ScheduleOccasion(PetState.ClockOut, ScheduleClock.ClockOutWeekday));
    }

    /// <summary>
    /// The end of the working week is told apart.
    /// </summary>
    [Fact]
    public void Update_ClockOutOnFriday_ReturnsClockOutFriday()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 13, 18, 1));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().Be(new ScheduleOccasion(PetState.ClockOut, ScheduleClock.ClockOutFriday));
    }

    /// <summary>
    /// A changed clock-out time is followed.
    /// </summary>
    [Fact]
    public void Update_CustomClockOutTime_FollowsIt()
    {
        // Arrange
        _options.ClockOutTime = new TimeSpan(16, 30, 0);
        _time.SetUtcNow(At(2026, 3, 11, 16, 31));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().Be(new ScheduleOccasion(PetState.ClockOut, ScheduleClock.ClockOutWeekday));
    }

    /// <summary>
    /// Long after the working day ended it is no longer the moment to say so.
    /// </summary>
    [Fact]
    public void Update_LongAfterClockOutTime_ReturnsNull()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 20, 0));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().BeNull();
    }

    /// <summary>
    /// A meal and the end of the day that fall together come one after the other, not at once.
    /// </summary>
    [Fact]
    public void Update_TwoOccasionsDue_SecondWaitsForCooldown()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 18, 0));

        // Act
        var first = _clock.Update();
        var tooSoon = _clock.Update();
        _time.Advance(TimeSpan.FromSeconds(25));
        var second = _clock.Update();

        // Assert
        first!.Value.State.Should().Be(PetState.ClockOut);
        tooSoon.Should().BeNull();
        second!.Value.State.Should().Be(PetState.Meal);
    }

    /// <summary>
    /// The occasions of one day come again the next day.
    /// </summary>
    [Fact]
    public void Update_NextDay_OccasionsComeAgain()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 12, 0));
        _clock.Update();
        _time.SetUtcNow(At(2026, 3, 12, 12, 0));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().Be(new ScheduleOccasion(PetState.Meal, ScheduleClock.Lunch));
    }

    /// <summary>
    /// A festival is celebrated once, with the festival as the variant.
    /// </summary>
    [Fact]
    public void Update_Festival_ReturnsFestiveOnce()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 10, 24, 9, 0));

        // Act
        var first = _clock.Update();
        _time.Advance(TimeSpan.FromMinutes(1));
        var second = _clock.Update();

        // Assert
        first.Should().Be(new ScheduleOccasion(PetState.Festive, (int)Festival.Programmer));
        second.Should().BeNull();
    }

    /// <summary>
    /// The chime is off until the user turns it on.
    /// </summary>
    [Fact]
    public void Update_FullHourWithChimeOff_ReturnsNull()
    {
        // Arrange
        _time.SetUtcNow(At(2026, 3, 11, 10, 0));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().BeNull();
    }

    /// <summary>
    /// With the chime on, each full hour sounds once and carries the hour.
    /// </summary>
    [Fact]
    public void Update_FullHourWithChimeOn_ReturnsChimeOncePerHour()
    {
        // Arrange
        _switches.Disabled = [];
        _time.SetUtcNow(At(2026, 3, 11, 10, 0));

        // Act
        var first = _clock.Update();
        _time.Advance(TimeSpan.FromSeconds(30));
        var second = _clock.Update();
        _time.SetUtcNow(At(2026, 3, 11, 11, 0));
        var third = _clock.Update();

        // Assert
        first.Should().Be(new ScheduleOccasion(PetState.Chime, 10));
        second.Should().BeNull();
        third.Should().Be(new ScheduleOccasion(PetState.Chime, 11));
    }

    /// <summary>
    /// The chime stays quiet at night.
    /// </summary>
    [Fact]
    public void Update_FullHourAtNight_ReturnsNull()
    {
        // Arrange
        _switches.Disabled = [];
        _time.SetUtcNow(At(2026, 3, 11, 23, 0));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().BeNull();
    }

    /// <summary>
    /// A switched-off scenario never comes up.
    /// </summary>
    [Fact]
    public void Update_MealSwitchedOff_ReturnsNull()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.Meal), nameof(PetState.Chime)];
        _time.SetUtcNow(At(2026, 3, 11, 12, 0));

        // Act
        var occasion = _clock.Update();

        // Assert
        occasion.Should().BeNull();
    }

    /// <summary>
    /// Builds an instant for a wall-clock time, as the fake time source runs in UTC.
    /// </summary>
    /// <param name="year">
    /// The year.
    /// </param>
    /// <param name="month">
    /// The month.
    /// </param>
    /// <param name="day">
    /// The day.
    /// </param>
    /// <param name="hour">
    /// The hour.
    /// </param>
    /// <param name="minute">
    /// The minute.
    /// </param>
    /// <returns>
    /// The instant.
    /// </returns>
    private static DateTimeOffset At(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }
}
