namespace DeskBuddy.Domain.Activity;

/// <summary>
/// Decides whether the user is in a meeting: a meeting application is in front.
/// </summary>
public sealed class MeetingPolicy
{
    /// <summary>
    /// Gets or sets the names of the processes (without <c>.exe</c>) that count as meeting applications.
    /// </summary>
    public IReadOnlyList<string> Processes { get; set; } = [];

    /// <summary>
    /// Tells whether the application in front is a meeting application.
    /// </summary>
    /// <param name="foregroundProcess">
    /// The name of the process owning the foreground window without <c>.exe</c>, or <see langword="null"/> if unknown.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if it is in the list, ignoring case.
    /// </returns>
    public bool IsInMeeting(string? foregroundProcess)
    {
        return foregroundProcess is not null && Processes.Contains(foregroundProcess, StringComparer.OrdinalIgnoreCase);
    }
}
