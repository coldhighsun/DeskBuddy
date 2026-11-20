namespace DeskBuddy.App.Presentation;

/// <summary>
/// Decides which culture the user interface is shown in.
/// </summary>
public static class LanguageResolver
{
    /// <summary>
    /// Picks the interface culture for a language setting.
    /// </summary>
    /// <param name="language">
    /// The chosen language.
    /// </param>
    /// <param name="system">
    /// The language of the operating system, used when the choice is <see cref="AppLanguage.Auto"/>.
    /// </param>
    /// <returns>
    /// Simplified Chinese, Traditional Chinese or English: a Chinese system language that is written in traditional
    /// characters (Taiwan, Hong Kong, Macao, or the <c>zh-Hant</c> script) gets Traditional Chinese, any other Chinese gets
    /// Simplified Chinese, and any other system language gets English.
    /// </returns>
    public static CultureInfo Resolve(AppLanguage language, CultureInfo system)
    {
        ArgumentNullException.ThrowIfNull(system);

        var name = language switch
        {
            AppLanguage.ZhCN => "zh-CN",
            AppLanguage.ZhHant => "zh-Hant",
            AppLanguage.En => "en",
            _ => FromSystem(system),
        };

        return CultureInfo.GetCultureInfo(name);
    }

    private static string FromSystem(CultureInfo system)
    {
        if (!string.Equals(system.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        for (var current = system; current.Name.Length > 0; current = current.Parent)
        {
            if (current.Name.Contains("Hant", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hant";
            }
        }

        var region = system.Name.Split('-')[^1];

        return region.ToUpperInvariant() is "TW" or "HK" or "MO" ? "zh-Hant" : "zh-CN";
    }
}
