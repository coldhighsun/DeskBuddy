using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace DeskBuddy.Infrastructure.Startup;

/// <summary>
/// Starts the application at sign-in through the current user's <c>Run</c> registry key, which needs no administrator
/// rights. Windows also keeps a per-entry "approved" flag that Task Manager's Startup tab toggles; an entry the user
/// disabled there counts as off here, and turning it on again clears that flag.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryAutoStartService : IAutoStartService
{
    /// <summary>
    /// Registry path, under the current user, of the key that lists programs started at sign-in.
    /// </summary>
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Registry path, under the current user, of the key holding the flags Task Manager sets for the entries of the
    /// <c>Run</c> key.
    /// </summary>
    public const string DefaultApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>
    /// Name of the value that represents this application in both keys.
    /// </summary>
    public const string ValueName = "DeskBuddy";

    /// <summary>
    /// The key that lists programs started at sign-in.
    /// </summary>
    private readonly string _runKeyPath;

    /// <summary>
    /// The key that holds the Task Manager flags.
    /// </summary>
    private readonly string _approvedKeyPath;

    /// <summary>
    /// The program to start, or <see langword="null"/> if it cannot be determined.
    /// </summary>
    private readonly string? _executablePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryAutoStartService"/> class.
    /// </summary>
    /// <param name="executablePath">
    /// The program to start at sign-in; <see langword="null"/> uses the running program.
    /// </param>
    /// <param name="runKeyPath">
    /// Registry path, under the current user, of the key that lists programs started at sign-in.
    /// </param>
    /// <param name="approvedKeyPath">
    /// Registry path, under the current user, of the key that holds the Task Manager flags.
    /// </param>
    public RegistryAutoStartService(
        string? executablePath = null,
        string runKeyPath = DefaultRunKeyPath,
        string approvedKeyPath = DefaultApprovedKeyPath)
    {
        _executablePath = executablePath ?? Environment.ProcessPath;
        _runKeyPath = runKeyPath;
        _approvedKeyPath = approvedKeyPath;
    }

    /// <inheritdoc />
    public bool IsEnabled()
    {
        if (_executablePath is null)
        {
            return false;
        }

        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(_runKeyPath);
            if (run?.GetValue(ValueName) is not string command
                || !string.Equals(command, Quote(_executablePath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // The first byte of the flag is odd when the user disabled the entry.
            using var approved = Registry.CurrentUser.OpenSubKey(_approvedKeyPath);
            return approved?.GetValue(ValueName) is not byte[] { Length: > 0 } flag || (flag[0] & 1) == 0;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool TrySet(bool enabled, out string? error)
    {
        if (_executablePath is null)
        {
            error = "The location of the running program is unknown.";
            return false;
        }

        try
        {
            if (enabled)
            {
                using var run = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);
                run.SetValue(ValueName, Quote(_executablePath), RegistryValueKind.String);

                // Clear the "disabled in Task Manager" flag so the entry takes effect again.
                using var approved = Registry.CurrentUser.OpenSubKey(_approvedKeyPath, writable: true);
                approved?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            else
            {
                using var run = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
                run?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            error = ex.Message;
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Wraps a path in quotes so a path with spaces is read as one program name.
    /// </summary>
    /// <param name="path">
    /// The path to wrap.
    /// </param>
    /// <returns>
    /// The quoted path.
    /// </returns>
    private static string Quote(string path)
    {
        return $"\"{path}\"";
    }
}
