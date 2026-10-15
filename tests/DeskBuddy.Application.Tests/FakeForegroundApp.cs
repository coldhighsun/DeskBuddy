namespace DeskBuddy.Application.Tests;

/// <summary>
/// Foreground application stub with a settable process name that counts how often it is asked.
/// </summary>
internal sealed class FakeForegroundApp : IForegroundAppDetector
{
    /// <summary>
    /// Gets or sets the process name to report, or <see langword="null"/> for none.
    /// </summary>
    public string? ProcessName { get; set; }

    /// <summary>
    /// Gets how many times the foreground process was looked up.
    /// </summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public string? GetForegroundProcessName()
    {
        QueryCount++;
        return ProcessName;
    }
}
