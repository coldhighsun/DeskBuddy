namespace DeskBuddy.Domain.Sedentary;

/// <summary>
/// Timings of the "you have been sitting too long" reminder.
/// </summary>
public sealed class SedentaryOptions
{
    /// <summary>
    /// Gets or sets how long the user can work without a real break before being reminded.
    /// </summary>
    public TimeSpan ReminderAfter { get; set; } = TimeSpan.FromMinutes(45);

    /// <summary>
    /// Gets or sets how long the keyboard and mouse must stay untouched for the user to count as having taken a
    /// break, which starts the work streak over.
    /// </summary>
    public TimeSpan RestThreshold { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long to wait before reminding again after the user asks to be reminded later.
    /// </summary>
    public TimeSpan SnoozeDuration { get; set; } = TimeSpan.FromMinutes(5);
}
