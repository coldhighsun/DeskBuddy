using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Schedule;

namespace DeskBuddy.Application.Scenarios;

/// <summary>
/// What <see cref="SystemScenarios.Evaluate"/> found for one moment.
/// </summary>
/// <param name="Ambient">
/// The state the pet should hold for as long as it applies (a meeting, a low battery), or <see langword="null"/>.
/// </param>
/// <param name="Moment">
/// A one-off state the pet should act out right now, with how long, or <see langword="null"/>.
/// </param>
/// <param name="Remark">
/// The state whose short remark should be said right now, or <see langword="null"/>.
/// </param>
/// <param name="RemarkVariant">
/// Which wording of the remark to use, for states that have several.
/// </param>
public readonly record struct SystemScenarioResult(
    PetState? Ambient,
    (PetState State, TimeSpan Duration)? Moment,
    PetState? Remark,
    int RemarkVariant = 0);

/// <summary>
/// What the pet does because of things around the user rather than their work: a meeting going on in front, the
/// user coming back after a long time, the battery running low or the cable being plugged in, and the clock: mealtimes,
/// the end of the working day, the full hour and festivals.
/// </summary>
/// <param name="foregroundApp">
/// Tells which application is in front.
/// </param>
/// <param name="power">
/// Tells how the computer is powered.
/// </param>
/// <param name="meeting">
/// Decides whether a meeting application is in front.
/// </param>
/// <param name="chat">
/// Decides whether a chat application is in front.
/// </param>
/// <param name="welcome">
/// Notices the user coming back.
/// </param>
/// <param name="powerPolicy">
/// Watches the battery and the cable.
/// </param>
/// <param name="clock">
/// Knows the times of day and dates the pet reacts to.
/// </param>
/// <param name="switches">
/// Which scenarios the user has switched on.
/// </param>
public sealed class SystemScenarios(
    IForegroundAppDetector foregroundApp,
    IPowerStatusProvider power,
    MeetingPolicy meeting,
    ChatPolicy chat,
    WelcomeBackPolicy welcome,
    PowerPolicy powerPolicy,
    ScheduleClock clock,
    ScenarioSwitches switches)
{
    /// <summary>
    /// How long the pet waves hello.
    /// </summary>
    public static readonly TimeSpan WelcomeDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long the pet charges up after the cable is plugged in.
    /// </summary>
    public static readonly TimeSpan ChargingDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the pet acts out a mealtime.
    /// </summary>
    public static readonly TimeSpan MealDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the pet acts out the end of the working day.
    /// </summary>
    public static readonly TimeSpan ClockOutDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the pet strikes its bell on the hour.
    /// </summary>
    public static readonly TimeSpan ChimeDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long the pet celebrates a festival.
    /// </summary>
    public static readonly TimeSpan FestiveDuration = TimeSpan.FromSeconds(6);

    /// <summary>
    /// How long the user may be away from keyboard and mouse and still be there to notice the time-based scenarios.
    /// </summary>
    private static readonly TimeSpan SchedulePresenceLimit = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether the battery was low at the previous evaluation, to say its remark only when it becomes low, not each
    /// time a meeting stops hiding it.
    /// </summary>
    private bool _wasLowBattery;

    /// <summary>
    /// Reads the sensors and works out what the pet should do about them.
    /// </summary>
    /// <param name="userIdle">
    /// How long the user has had no keyboard or mouse input.
    /// </param>
    /// <param name="canDisturb">
    /// Whether the pet may draw attention to itself now: it is on screen and nothing full-screen or quiet is in front.
    /// </param>
    /// <returns>
    /// What the pet should do.
    /// </returns>
    public SystemScenarioResult Evaluate(TimeSpan userIdle, bool canDisturb = true)
    {
        var active = new List<PetState>(2);
        (PetState State, TimeSpan Duration)? moment = null;
        PetState? remark = null;
        var remarkVariant = 0;

        var wantsMeeting = switches.Allows(PetState.Meeting);
        var wantsChatting = switches.Allows(PetState.Chatting);
        if (wantsMeeting || wantsChatting)
        {
            var foreground = foregroundApp.GetForegroundProcessName();
            if (wantsMeeting && meeting.IsInMeeting(foreground))
            {
                active.Add(PetState.Meeting);
            }

            if (wantsChatting && chat.IsChatting(foreground))
            {
                active.Add(PetState.Chatting);
            }
        }

        if (!switches.Allows(PetState.Welcome))
        {
            welcome.Reset();
        }

        if (switches.Allows(PetState.Welcome) && welcome.Update(userIdle))
        {
            moment = (PetState.Welcome, WelcomeDuration);
            remark = PetState.Welcome;
        }

        var wantsLowBattery = switches.Allows(PetState.LowBattery);
        var wantsCharging = switches.Allows(PetState.Charging);
        if (!wantsLowBattery && !wantsCharging)
        {
            powerPolicy.Reset();
        }

        if (wantsLowBattery || wantsCharging)
        {
            var change = powerPolicy.Update(power.GetStatus());
            if (wantsLowBattery && change.IsLow)
            {
                active.Add(PetState.LowBattery);
            }

            if (wantsCharging && change.JustPluggedIn)
            {
                moment ??= (PetState.Charging, ChargingDuration);
            }
        }

        // The clock only speaks up for a user who is at the computer, outside meetings, and while the pet may be seen;
        // otherwise the occasion stays unused and comes later, if it is still the time for it.
        if (moment is null
            && canDisturb
            && userIdle < SchedulePresenceLimit
            && !active.Contains(PetState.Meeting)
            && clock.Update() is { } occasion)
        {
            moment = (occasion.State, DurationOf(occasion.State));
            remark = occasion.State;
            remarkVariant = occasion.Variant;
        }

        var ambient = ScenarioCatalog.AmbientRanking.Pick(active);
        var isLowBattery = active.Contains(PetState.LowBattery);
        if (ambient == PetState.LowBattery && !_wasLowBattery)
        {
            remark ??= PetState.LowBattery;
        }

        _wasLowBattery = isLowBattery;

        return new SystemScenarioResult(ambient, moment, remark, remarkVariant);
    }

    /// <summary>
    /// Tells how long a time-based state is acted out.
    /// </summary>
    /// <param name="state">
    /// The state.
    /// </param>
    /// <returns>
    /// The duration.
    /// </returns>
    private static TimeSpan DurationOf(PetState state)
    {
        return state switch
        {
            PetState.Meal => MealDuration,
            PetState.ClockOut => ClockOutDuration,
            PetState.Chime => ChimeDuration,
            _ => FestiveDuration,
        };
    }
}
