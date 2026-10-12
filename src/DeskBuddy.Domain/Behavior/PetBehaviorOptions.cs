namespace DeskBuddy.Domain.Behavior;

/// <summary>
/// Tunable timings and speeds of the pet's autonomous behavior.
/// </summary>
public sealed class PetBehaviorOptions
{
    /// <summary>
    /// Gets or sets how long the user must be away from keyboard and mouse before the pet falls asleep.
    /// </summary>
    public TimeSpan SleepAfter { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the shortest time the pet stands still before it may start walking.
    /// </summary>
    public TimeSpan IdleDwellMin { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets or sets the longest time the pet stands still before it starts walking.
    /// </summary>
    public TimeSpan IdleDwellMax { get; set; } = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Gets or sets the shortest duration of a walk.
    /// </summary>
    public TimeSpan WalkDurationMin { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the longest duration of a walk.
    /// </summary>
    public TimeSpan WalkDurationMax { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Gets or sets how long the pet stays happy after being clicked.
    /// </summary>
    public TimeSpan HappyDuration { get; set; } = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Gets or sets how long a speech bubble stays up before it fades on its own.
    /// </summary>
    public TimeSpan MessageDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how recently the user must have used keyboard or mouse for a development tool in front to count
    /// as development work.
    /// </summary>
    public TimeSpan DevActiveWindow { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long the conditions for development work must stay unmet before the pet gets up from its
    /// computer, so a quick look at a browser does not make it jump up and sit down again.
    /// </summary>
    public TimeSpan DevExitGrace { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets or sets the processor load, in percent, a build tool must cause for a build to be recognized.
    /// </summary>
    public double BuildStartCpu { get; set; } = 60;

    /// <summary>
    /// Gets or sets how long the load must stay at <see cref="BuildStartCpu"/> before a build is recognized.
    /// </summary>
    public TimeSpan BuildStartAfter { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the processor load, in percent, below which a recognized build counts as pausing.
    /// </summary>
    public double BuildContinueCpu { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long a build may pause (build tools gone or the processor quiet) before it counts as over.
    /// </summary>
    public TimeSpan BuildEndGrace { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets or sets the shortest build that is celebrated when it ends.
    /// </summary>
    public TimeSpan BuildMinDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the shortest time between two parcels for git traffic.
    /// </summary>
    public TimeSpan GitCooldown { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long the user must have been developing without a real break before the late-night remark and
    /// look.
    /// </summary>
    public TimeSpan NightCodingAfter { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Gets or sets how long the user must be away from development before the session counts as over, so that
    /// looking something up in a browser or a short pause does not restart the count for the late-night remark.
    /// </summary>
    public TimeSpan DevSessionBreak { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how soon after developing a look in the browser counts as reading up on something.
    /// </summary>
    public TimeSpan ReadingWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long the user must have been away before coming back is welcomed.
    /// </summary>
    public TimeSpan WelcomeAwayAfter { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the battery charge, in percent, at or below which the battery counts as low.
    /// </summary>
    public int LowBatteryPercent { get; set; } = 20;

    /// <summary>
    /// Gets or sets the charge, in percent, a low battery must reach again before it stops counting as low.
    /// </summary>
    public int LowBatteryRecoverPercent { get; set; } = 25;

    /// <summary>
    /// Gets or sets the time of day, by the local clock, at which the working day ends.
    /// </summary>
    public TimeSpan ClockOutTime { get; set; } = TimeSpan.FromHours(18);

    /// <summary>
    /// Gets or sets the walking speed in device-independent pixels per second.
    /// </summary>
    public double WalkSpeed { get; set; } = 36;

    /// <summary>
    /// Gets or sets the smallest height, in device-independent pixels, a dropped pet must be above where it would
    /// settle to fall instead of settling at once.
    /// </summary>
    public double FallMinHeight { get; set; } = 160;

    /// <summary>
    /// Gets or sets the downward acceleration of a falling pet in device-independent pixels per second squared.
    /// </summary>
    public double FallGravity { get; set; } = 900;

    /// <summary>
    /// Gets or sets the fastest speed, in device-independent pixels per second, a falling pet reaches once its umbrella
    /// or parachute is open.
    /// </summary>
    public double FallTerminalVelocity { get; set; } = 100;

    /// <summary>
    /// Gets or sets the speed, in device-independent pixels per second, at which the umbrella or parachute opens.
    /// </summary>
    public double FallOpenSpeed { get; set; } = 240;

    /// <summary>
    /// Gets or sets how fast, in device-independent pixels per second squared, the open umbrella or parachute slows
    /// the pet down to <see cref="FallTerminalVelocity"/>.
    /// </summary>
    public double FallBrake { get; set; } = 500;

    /// <summary>
    /// Gets or sets the chance, from 0 to 1, that a pet walking into the left or right end of the screen starts to
    /// climb it, when climbing is switched on.
    /// </summary>
    public double ClimbChance { get; set; } = 0.3;

    /// <summary>
    /// Gets or sets the speed, in device-independent pixels per second, at which the pet climbs and crawls along the
    /// top edge.
    /// </summary>
    public double ClimbSpeed { get; set; } = 30;

    /// <summary>
    /// Gets or sets the shortest time between two moments at which a pet crawling along the top edge may turn around.
    /// </summary>
    public TimeSpan CeilingTurnIntervalMin { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the longest time between two moments at which a pet crawling along the top edge may turn around.
    /// </summary>
    public TimeSpan CeilingTurnIntervalMax { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the chance, from 0 to 1, that the pet turns around at such a moment.
    /// </summary>
    public double CeilingReverseChance { get; set; } = 0.35;

    /// <summary>
    /// Gets or sets the shortest time the pet crawls along the top edge before it lets go or climbs down.
    /// </summary>
    public TimeSpan CeilingDurationMin { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Gets or sets the longest time the pet crawls along the top edge before it lets go or climbs down.
    /// </summary>
    public TimeSpan CeilingDurationMax { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the chance, from 0 to 1, that the pet lets go of the top edge and falls when its time there is up,
    /// instead of crawling to a corner and climbing down.
    /// </summary>
    public double CeilingDropChance { get; set; } = 0.5;
}
