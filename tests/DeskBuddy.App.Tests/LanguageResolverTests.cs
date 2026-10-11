using System.Globalization;
using AwesomeAssertions;
using DeskBuddy.App.Presentation;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="LanguageResolver"/>.
/// </summary>
public class LanguageResolverTests
{
    /// <summary>
    /// Following the system picks Chinese for every Chinese system language and English for all others.
    /// </summary>
    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
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
    public void Resolve_ChosenLanguage_IgnoresTheSystem(AppLanguage language, string system, string expected)
    {
        // Act
        var culture = LanguageResolver.Resolve(language, CultureInfo.GetCultureInfo(system));

        // Assert
        culture.Name.Should().Be(expected);
    }
}
