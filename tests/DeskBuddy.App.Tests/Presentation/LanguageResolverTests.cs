using System.Globalization;

namespace DeskBuddy.App.Tests.Presentation;

/// <summary>
/// Tests for <see cref="LanguageResolver"/>.
/// </summary>
public class LanguageResolverTests
{
    /// <summary>
    /// Following the system picks Traditional Chinese for the traditional Chinese regions, Simplified Chinese for the
    /// other Chinese system languages and English for all others.
    /// </summary>
    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-SG", "zh-CN")]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-MO", "zh-Hant")]
    [InlineData("zh-Hant", "zh-Hant")]
    [InlineData("en-GB", "en")]
    [InlineData("ja-JP", "en")]
    [InlineData("de-DE", "en")]
    public void Resolve_Auto_FollowsTheSystemLanguage(string system, string expected)
    {
        // Act
        var culture = LanguageResolver.Resolve(AppLanguage.Auto, CultureInfo.GetCultureInfo(system));

        // Assert
        culture.Name.Should().Be(expected);
    }

    /// <summary>
    /// A chosen language wins over the system language.
    /// </summary>
    [Theory]
    [InlineData(AppLanguage.ZhCN, "en-US", "zh-CN")]
    [InlineData(AppLanguage.En, "zh-CN", "en")]
    [InlineData(AppLanguage.ZhHant, "en-US", "zh-Hant")]
    [InlineData(AppLanguage.ZhHant, "zh-CN", "zh-Hant")]
    [InlineData(AppLanguage.ZhCN, "zh-TW", "zh-CN")]
    public void Resolve_ChosenLanguage_IgnoresTheSystem(AppLanguage language, string system, string expected)
    {
        // Act
        var culture = LanguageResolver.Resolve(language, CultureInfo.GetCultureInfo(system));

        // Assert
        culture.Name.Should().Be(expected);
    }
}
