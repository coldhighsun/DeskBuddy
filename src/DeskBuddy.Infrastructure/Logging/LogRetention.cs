using System.Globalization;

namespace DeskBuddy.Infrastructure.Logging;

/// <summary>
/// Names the daily log files and deletes the ones that are too old.
/// </summary>
public static class LogRetention
{
    /// <summary>
    /// Prefix of every log file name.
    /// </summary>
    private const string Prefix = "deskbuddy-";

    /// <summary>
    /// Extension of every log file name.
    /// </summary>
    private const string Extension = ".log";

    /// <summary>
    /// Date format used in log file names.
    /// </summary>
    private const string DateFormat = "yyyyMMdd";

    /// <summary>
    /// Builds the file name of the log for a day.
    /// </summary>
    /// <param name="date">
    /// The day the log belongs to.
    /// </param>
    /// <returns>
    /// The file name, without a directory.
    /// </returns>
    public static string FileNameFor(DateOnly date)
    {
        return $"{Prefix}{date.ToString(DateFormat, CultureInfo.InvariantCulture)}{Extension}";
    }

    /// <summary>
    /// Deletes log files older than the retention window. The age comes from the date in the file name, and files that are
    /// not DeskBuddy logs are left alone. A file that cannot be deleted is skipped.
    /// </summary>
    /// <param name="directory">
    /// The log directory; a missing directory is ignored.
    /// </param>
    /// <param name="today">
    /// The current day.
    /// </param>
    /// <param name="keepDays">
    /// How many days, including today, are kept.
    /// </param>
    public static void Prune(string directory, DateOnly today, int keepDays)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var oldest = today.AddDays(1 - keepDays);
        foreach (var path in Directory.EnumerateFiles(directory, $"{Prefix}*{Extension}"))
        {
            var name = Path.GetFileNameWithoutExtension(path)[Prefix.Length..];
            if (!DateOnly.TryParseExact(name, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || date >= oldest)
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A log still open elsewhere is simply kept until the next start.
            }
        }
    }
}
