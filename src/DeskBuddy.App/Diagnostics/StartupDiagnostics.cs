using System.Diagnostics;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Diagnostics;

/// <summary>
/// Start-up chores that concern the process rather than the pet: how much CPU it may take, and what it records about
/// the machine it runs on.
/// </summary>
internal static class StartupDiagnostics
{
    /// <summary>
    /// Lowers the priority of the process. A desktop pet must never compete with the user's real work (builds, games,
    /// meetings) for the CPU. <see cref="ProcessPriorityClass.BelowNormal"/> rather than Idle, so the animation does not
    /// stall completely while the machine is busy.
    /// </summary>
    /// <param name="logger">Reports a priority that could not be changed.</param>
    public static void LowerPriority(ILogger logger)
    {
        try
        {
            using var self = Process.GetCurrentProcess();
            self.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Purely an optimisation: keep running at the normal priority.
            logger.LogWarning(ex, "Could not lower the process priority.");
        }
    }

    /// <summary>
    /// Records what the application runs on, so a log sent in with a problem tells the version, the system and the
    /// monitor layout without asking.
    /// </summary>
    /// <param name="logger">Where to write it.</param>
    public static void LogEnvironment(ILogger logger)
    {
        logger.LogInformation(
            "DeskBuddy {Version} on {OS} ({Architecture}), .NET {Runtime}.",
            AboutInfo.CurrentVersion(),
            Environment.OSVersion.VersionString,
            RuntimeInformation.OSArchitecture,
            Environment.Version);
        foreach (var monitor in MonitorHelper.GetAllMonitors())
        {
            logger.LogInformation(
                "Monitor {Bounds}, work area {WorkArea}, scale {Scale:P0}.",
                monitor.Bounds,
                monitor.WorkArea,
                monitor.Scale);
        }
    }
}
