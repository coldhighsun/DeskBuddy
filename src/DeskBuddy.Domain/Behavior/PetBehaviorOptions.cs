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
    /// Gets or sets the walking speed in device-independent pixels per second.
    /// </summary>
    public double WalkSpeed { get; set; } = 60;
}
