using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.Domain.Schedule;

/// <summary>
/// Something the pet acts out because of the time of day or the date.
/// </summary>
/// <param name="State">
/// The state to act out: <see cref="PetState.Meal"/>, <see cref="PetState.ClockOut"/>, <see cref="PetState.Chime"/> or
/// <see cref="PetState.Festive"/>.
/// </param>
/// <param name="Variant">
/// Tells which wording goes with it: for a meal 1 (lunch) or 2 (dinner), for clocking out 1 or 2 (Friday), for a
/// festive day the <see cref="Festival"/>, for the chime the hour.
/// </param>
public readonly record struct ScheduleOccasion(PetState State, int Variant);

/// <summary>
/// Knows the times of day and dates the pet reacts to: mealtimes, the end of the working day, the full hour and
/// festivals. Except for the chime each of them comes once a day.
/// </summary>
/// <param name="options">
/// When the working day ends.
/// </param>
/// <param name="timeProvider">
/// The time source; the local clock decides.
/// </param>
/// <param name="switches">
/// Which scenarios the user has switched on; an occasion that is switched off never comes up.
/// </param>
public sealed class ScheduleClock(PetBehaviorOptions options, TimeProvider timeProvider, ScenarioSwitches switches)
{
    /// <summary>
    /// The variant of a lunch break.
    /// </summary>
    public const int Lunch = 1;

    /// <summary>
    /// The variant of an evening meal.
    /// </summary>
    public const int Dinner = 2;

    /// <summary>
    /// The variant of an ordinary end of the working day.
    /// </summary>
    public const int ClockOutWeekday = 1;

    /// <summary>
    /// The variant of the end of the working week.
    /// </summary>
    public const int ClockOutFriday = 2;

    /// <summary>
    /// The first hour the chime sounds.
    /// </summary>
    private const int ChimeFirstHour = 8;

    /// <summary>
    /// The last hour the chime sounds.
    /// </summary>
    private const int ChimeLastHour = 22;

    /// <summary>
    /// How long after the clock-out time it still counts as the end of the working day.
    /// </summary>
    private static readonly TimeSpan ClockOutWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The shortest time between two occasions, so one does not cut the other short.
    /// </summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    /// <summary>
    /// When lunch time starts.
    /// </summary>
    private static readonly TimeSpan LunchStart = new(11, 30, 0);

    /// <summary>
    /// When lunch time ends.
    /// </summary>
    private static readonly TimeSpan LunchEnd = new(13, 0, 0);

    /// <summary>
    /// When the evening meal time starts.
    /// </summary>
    private static readonly TimeSpan DinnerStart = new(17, 30, 0);

    /// <summary>
    /// When the evening meal time ends.
    /// </summary>
    private static readonly TimeSpan DinnerEnd = new(19, 0, 0);

    /// <summary>
    /// The day the once-a-day flags belong to.
    /// </summary>
    private DateOnly _day;

    /// <summary>
    /// Whether lunch was announced today.
    /// </summary>
    private bool _lunchDone;

    /// <summary>
    /// Whether the evening meal was announced today.
    /// </summary>
    private bool _dinnerDone;

    /// <summary>
    /// Whether the end of the working day was announced today.
    /// </summary>
    private bool _clockOutDone;

    /// <summary>
    /// Whether today's festival was celebrated.
    /// </summary>
    private bool _festivalDone;

    /// <summary>
    /// The festival today is, worked out once per day.
    /// </summary>
    private Festival _festival;

    /// <summary>
    /// The date and hour of the last chime, to chime once per hour.
    /// </summary>
    private DateTime _lastChime;

    /// <summary>
    /// When the last occasion came up.
    /// </summary>
    private DateTimeOffset _lastOccasion = DateTimeOffset.MinValue;

    /// <summary>
    /// Checks the clock. Call only when the pet may act something out now (the user is at the computer and nothing
    /// forbids it): an occasion that comes up is used up, one that does not wait for the next call.
    /// </summary>
    /// <returns>
    /// The occasion to act out, or <see langword="null"/> if there is none.
    /// </returns>
    public ScheduleOccasion? Update()
    {
        var now = timeProvider.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        if (today != _day)
        {
            _day = today;
            _lunchDone = _dinnerDone = _clockOutDone = _festivalDone = false;
            _festival = Festivals.On(today);
        }

        if (now - _lastOccasion < Cooldown)
        {
            return null;
        }

        var occasion = FindOccasion(now);
        if (occasion is not null)
        {
            _lastOccasion = now;
        }

        return occasion;
    }

    /// <summary>
    /// Finds the occasion that applies now and uses it up.
    /// </summary>
    /// <param name="now">
    /// The local time.
    /// </param>
    /// <returns>
    /// The occasion, or <see langword="null"/> if there is none.
    /// </returns>
    private ScheduleOccasion? FindOccasion(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.DateTime);
        var time = now.TimeOfDay;
        if (!_festivalDone && _festival != Festival.None && switches.Allows(PetState.Festive))
        {
            _festivalDone = true;
            return new ScheduleOccasion(PetState.Festive, (int)_festival);
        }

        var clockOut = options.ClockOutTime;
        if (!_clockOutDone && switches.Allows(PetState.ClockOut) && time >= clockOut && time < clockOut + ClockOutWindow)
        {
            _clockOutDone = true;
            return new ScheduleOccasion(
                PetState.ClockOut,
                today.DayOfWeek == DayOfWeek.Friday ? ClockOutFriday : ClockOutWeekday);
        }

        if (switches.Allows(PetState.Meal))
        {
            if (!_lunchDone && time >= LunchStart && time < LunchEnd)
            {
                _lunchDone = true;
                return new ScheduleOccasion(PetState.Meal, Lunch);
            }

            if (!_dinnerDone && time >= DinnerStart && time < DinnerEnd)
            {
                _dinnerDone = true;
                return new ScheduleOccasion(PetState.Meal, Dinner);
            }
        }

        if (switches.Allows(PetState.Chime) && now.Minute == 0 && now.Hour is >= ChimeFirstHour and <= ChimeLastHour)
        {
            var hour = new DateTime(today.Year, today.Month, today.Day, now.Hour, 0, 0, DateTimeKind.Unspecified);
            if (hour != _lastChime)
            {
                _lastChime = hour;
                return new ScheduleOccasion(PetState.Chime, now.Hour);
            }
        }

        return null;
    }
}
