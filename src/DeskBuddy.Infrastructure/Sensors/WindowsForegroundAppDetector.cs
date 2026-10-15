namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Reads the name of the foreground window's process. Only the process name is read: never the window title or what
/// is typed into it.
/// </summary>
public sealed class WindowsForegroundAppDetector : IForegroundAppDetector
{
    /// <inheritdoc />
    public string? GetForegroundProcessName()
    {
        var window = ForegroundWindow.GetHandle();
        return window == 0 ? null : ForegroundWindow.GetProcessName(window);
    }
}
