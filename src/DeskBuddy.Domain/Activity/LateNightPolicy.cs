namespace DeskBuddy.Domain.Activity;

/// <summary>
/// Decides whether the user is still developing far into the night: between 23:00 and 05:00 by the local clock, after
/// a long session.
/// </summary>
/// <param name="options">
/// How long a session must have lasted.
/// </param>
/// <param name="timeProvider">
/// The time source; the local time zone decides what counts as night.
/// </param>
public sealed class LateNightPolicy(PetBehaviorOptions options, TimeProvider timeProvider)
{
    /// <summary>
    /// The hour at which the night begins.
    /// </summary>
    private const int NightStartHour = 23;

    /// <summary>
    /// The hour at which the night ends.
    /// </summary>
    private const int NightEndHour = 5;

    /// <summary>
    /// Tells whether it is a late-night session.
    /// </summary>
    /// <param name="developingFor">
    /// How long the user has been developing without a real break.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if it is night and the session has lasted long enough.
    /// </returns>
    public bool IsLateNightSession(TimeSpan developingFor)
    {
        if (developingFor < options.NightCodingAfter)
        {
            return false;
        }

        var hour = timeProvider.GetLocalNow().Hour;
        return hour >= NightStartHour || hour < NightEndHour;
    }
}
