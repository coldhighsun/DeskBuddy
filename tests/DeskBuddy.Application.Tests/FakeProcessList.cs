namespace DeskBuddy.Application.Tests;

/// <summary>
/// Process list stub with settable names that counts how often it is asked.
/// </summary>
internal sealed class FakeProcessList : IProcessList
{
    /// <summary>
    /// Gets or sets the process names to report.
    /// </summary>
    public HashSet<string> Names { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets how many times the list was read.
    /// </summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public IReadOnlySet<string> GetNames()
    {
        QueryCount++;
        return Names;
    }
}
