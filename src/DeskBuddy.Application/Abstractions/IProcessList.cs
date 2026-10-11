namespace DeskBuddy.Application.Abstractions;

/// <summary>
/// Tells which processes are running. Only names are looked at: never command lines, windows or what a process does.
/// </summary>
public interface IProcessList
{
    /// <summary>
    /// Gets the names of all running processes.
    /// </summary>
    /// <returns>
    /// The process names without <c>.exe</c>, compared ignoring case.
    /// </returns>
    IReadOnlySet<string> GetNames();
}
