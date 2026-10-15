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
    /// Simplified Chinese or English: any system language other than Chinese gets English.
    /// </returns>
    public static CultureInfo Resolve(AppLanguage language, CultureInfo system)
    {
        ArgumentNullException.ThrowIfNull(system);

        var chinese = language switch
        {
            AppLanguage.ZhCN => true,
            AppLanguage.En => false,
            _ => string.Equals(system.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase),
        };

        return CultureInfo.GetCultureInfo(chinese ? "zh-CN" : "en");
    }
}
