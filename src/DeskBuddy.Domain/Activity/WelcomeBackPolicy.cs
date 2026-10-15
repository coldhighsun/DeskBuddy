namespace DeskBuddy.Domain.Activity;

/// <summary>
/// Notices the user coming back after a long absence: no keyboard or mouse input for a long time, then input again.
/// </summary>
/// <param name="options">
/// How long an absence must be to be worth a welcome.
/// </param>
public sealed class WelcomeBackPolicy(PetBehaviorOptions options)
{
    /// <summary>
    /// Whether the user has been away long enough and has not been seen back yet.
    /// </summary>
    private bool _away;

    /// <summary>
    /// Forgets that the user was away, for when the readings stop coming (the scenario was switched off), so that
    /// switching it on again does not welcome back a user who never left.
    /// </summary>
    public void Reset()
    {
        _away = false;
    }

    /// <summary>
    /// Takes in the latest reading.
    /// </summary>
    /// <param name="userIdle">
    /// How long the user has had no keyboard or mouse input.
    /// </param>
    /// <returns>
    /// <see langword="true"/> once, when the user is back after being away for the configured time.
    /// </returns>
    public bool Update(TimeSpan userIdle)
    {
        if (userIdle >= options.WelcomeAwayAfter)
        {
            _away = true;
            return false;
        }

        if (!_away)
        {
            return false;
        }

        _away = false;
        return true;
    }
}
