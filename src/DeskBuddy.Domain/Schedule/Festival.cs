using System.Globalization;

namespace DeskBuddy.Domain.Schedule;

/// <summary>
/// The festivals the pet celebrates.
/// </summary>
public enum Festival
{
    /// <summary>
    /// No festival.
    /// </summary>
    None = 0,

    /// <summary>
    /// New Year's Day (1 January).
    /// </summary>
    NewYear = 1,

    /// <summary>
    /// Programmers' Day (24 October, the day of 1024).
    /// </summary>
    Programmer = 2,

    /// <summary>
    /// Christmas Day (25 December).
    /// </summary>
    Christmas = 3,

    /// <summary>
    /// Chinese New Year, the first day of the first lunar month.
    /// </summary>
    SpringFestival = 4,
}

/// <summary>
/// Finds out whether a date is a festival the pet celebrates.
/// </summary>
public static class Festivals
{
    /// <summary>
    /// Used to turn a date into a lunar one.
    /// </summary>
    private static readonly ChineseLunisolarCalendar LunarCalendar = new();

    /// <summary>
    /// Tells which festival, if any, a date is.
    /// </summary>
    /// <param name="date">
    /// The date, in the local calendar.
    /// </param>
    /// <returns>
    /// The festival, or <see cref="Festival.None"/>.
    /// </returns>
    public static Festival On(DateOnly date)
    {
        return (date.Month, date.Day) switch
        {
            (1, 1) => Festival.NewYear,
            (10, 24) => Festival.Programmer,
            (12, 25) => Festival.Christmas,
            _ => IsLunarNewYear(date) ? Festival.SpringFestival : Festival.None,
        };
    }

    /// <summary>
    /// Tells whether a date is the first day of the first lunar month.
    /// </summary>
    /// <param name="date">
    /// The date.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if it is; also if the date lies outside what the lunar calendar can convert, then
    /// <see langword="false"/>.
    /// </returns>
    private static bool IsLunarNewYear(DateOnly date)
    {
        // Lunar New Year falls between 21 January and 21 February; skip the conversion for all other dates.
        if (date.Month is not (1 or 2))
        {
            return false;
        }

        var moment = date.ToDateTime(TimeOnly.MinValue);
        if (moment < LunarCalendar.MinSupportedDateTime || moment > LunarCalendar.MaxSupportedDateTime)
        {
            return false;
        }

        return LunarCalendar.GetMonth(moment) == 1 && LunarCalendar.GetDayOfMonth(moment) == 1;
    }
}
