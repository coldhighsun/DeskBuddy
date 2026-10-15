namespace DeskBuddy.Domain.Activity;

/// <summary>
/// Decides whether the user is chatting: a chat application is in front.
/// </summary>
public sealed class ChatPolicy
{
    /// <summary>
    /// Gets or sets the names of the processes (without <c>.exe</c>) that count as chat applications.
    /// </summary>
    public IReadOnlyList<string> Processes { get; set; } = [];

    /// <summary>
    /// Tells whether the application in front is a chat application.
    /// </summary>
    /// <param name="foregroundProcess">
    /// The name of the process owning the foreground window without <c>.exe</c>, or <see langword="null"/> if unknown.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if it is in the list, ignoring case.
    /// </returns>
    public bool IsChatting(string? foregroundProcess)
    {
        return foregroundProcess is not null && Processes.Contains(foregroundProcess, StringComparer.OrdinalIgnoreCase);
    }
}
