using System.Drawing;

namespace DeskBuddy.App;

/// <summary>
/// Loads the application icon that is embedded in the assembly (the same file as the executable's icon).
/// </summary>
internal static partial class AppIcon
{
    /// <summary>
    /// The pack URI of the embedded icon. <c>PanelWindow.xaml</c> uses the same address, and the csproj embeds the file.
    /// </summary>
    public const string PackUri = "pack://application:,,,/Assets/DeskBuddy.ico";

    /// <summary>
    /// <c>GetSystemMetrics</c> index of the width of a small icon, the size the notification area draws.
    /// </summary>
    private const int SmallIconWidthMetric = 49;

    /// <summary>
    /// <c>GetSystemMetrics</c> index of the height of a small icon.
    /// </summary>
    private const int SmallIconHeightMetric = 50;

    /// <summary>
    /// Loads the icon at the system's small-icon size, so the notification area shows the matching frame of the file
    /// instead of scaling a larger one down.
    /// </summary>
    /// <returns>
    /// The icon; the caller disposes it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The icon is not embedded in the assembly.
    /// </exception>
    public static Icon LoadSmall()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri(PackUri))
            ?? throw new InvalidOperationException($"The application icon {PackUri} is not embedded in the assembly.");
        using var stream = resource.Stream;
        return new Icon(stream, new Size(GetSystemMetrics(SmallIconWidthMetric), GetSystemMetrics(SmallIconHeightMetric)));
    }

    /// <summary>
    /// Reads a system metric.
    /// </summary>
    /// <param name="index">
    /// The metric to read.
    /// </param>
    /// <returns>
    /// The value of the metric.
    /// </returns>
    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}
