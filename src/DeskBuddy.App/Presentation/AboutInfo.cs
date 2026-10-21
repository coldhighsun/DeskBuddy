using System.Reflection;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// The facts the about window shows: the version of the running application and where its project lives.
/// </summary>
public static class AboutInfo
{
    /// <summary>
    /// The address of the project page.
    /// </summary>
    public static readonly Uri ProjectUri = new("https://github.com/coldhighsun/DeskBuddy");

    /// <summary>
    /// The text shown when the version cannot be read.
    /// </summary>
    private const string UnknownVersion = "?";

    /// <summary>
    /// Reads the version of the running application, as MinVer set it from the git tag.
    /// </summary>
    /// <returns>
    /// The version text, such as <c>1.2.3</c>.
    /// </returns>
    public static string CurrentVersion()
    {
        var assembly = typeof(AboutInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return FormatVersion(informational);
    }

    /// <summary>
    /// Turns an informational version into the text to show, dropping the build metadata (such as the commit hash)
    /// after a <c>+</c>.
    /// </summary>
    /// <param name="informationalVersion">
    /// The informational version, or <see langword="null"/> if there is none.
    /// </param>
    /// <returns>
    /// The version without build metadata, or a question mark if there is no version.
    /// </returns>
    public static string FormatVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return UnknownVersion;
        }

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        var version = (plus >= 0 ? informationalVersion[..plus] : informationalVersion).Trim();
        return version.Length == 0 ? UnknownVersion : version;
    }
}
