using AwesomeAssertions;
using DeskBuddy.Domain.Schedule;

namespace DeskBuddy.Domain.Tests.Schedule;

/// <summary>
/// Tests for <see cref="Festivals"/>.
/// </summary>
public class FestivalsTests
{
    /// <summary>
    /// The fixed dates are recognized in every year.
    /// </summary>
    [Theory]
    [InlineData(2026, 1, 1, Festival.NewYear)]
    [InlineData(2027, 10, 24, Festival.Programmer)]
    [InlineData(2030, 12, 25, Festival.Christmas)]
    public void On_FixedDate_ReturnsFestival(int year, int month, int day, Festival expected)
    {
        // Act
        var festival = Festivals.On(new DateOnly(year, month, day));

        // Assert
        festival.Should().Be(expected);
    }

    /// <summary>
    /// Chinese New Year is found by the lunar calendar.
    /// </summary>
    [Theory]
    [InlineData(2025, 1, 29)]
    [InlineData(2026, 2, 17)]
    [InlineData(2027, 2, 6)]
    public void On_LunarNewYear_ReturnsSpringFestival(int year, int month, int day)
    {
        // Act
        var festival = Festivals.On(new DateOnly(year, month, day));

        // Assert
        festival.Should().Be(Festival.SpringFestival);
    }

    /// <summary>
    /// The days around a festival are ordinary days.
    /// </summary>
    [Theory]
    [InlineData(2026, 2, 16)]
    [InlineData(2026, 2, 18)]
    [InlineData(2026, 10, 23)]
    [InlineData(2026, 6, 1)]
    public void On_OrdinaryDay_ReturnsNone(int year, int month, int day)
    {
        // Act
        var festival = Festivals.On(new DateOnly(year, month, day));

        // Assert
        festival.Should().Be(Festival.None);
    }

    /// <summary>
    /// A date beyond what the lunar calendar can convert does not throw.
    /// </summary>
    [Fact]
    public void On_DateBeyondLunarCalendar_ReturnsNone()
    {
        // Act
        var festival = Festivals.On(new DateOnly(2150, 2, 1));

        // Assert
        festival.Should().Be(Festival.None);
    }
}
