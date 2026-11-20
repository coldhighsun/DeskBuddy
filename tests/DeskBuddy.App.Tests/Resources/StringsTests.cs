using System.Globalization;
using DeskBuddy.App.Resources;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Schedule;

namespace DeskBuddy.App.Tests.Resources;

/// <summary>
/// Tests for the lookups of <see cref="Strings"/> that choose a text from more than a name.
/// </summary>
public class StringsTests : IDisposable
{
    /// <summary>
    /// The interface culture before the test, put back afterwards.
    /// </summary>
    private readonly CultureInfo _original = CultureInfo.CurrentUICulture;

    /// <summary>
    /// Initializes a new instance of the <see cref="StringsTests"/> class; the texts are read in English.
    /// </summary>
    public StringsTests()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _original;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// While the application has not chosen a language, texts are looked up in the culture of the thread.
    /// </summary>
    [Fact]
    public void Current_NoLanguageChosen_FollowsTheCultureOfTheThread()
    {
        // Act
        var current = Strings.Current;

        // Assert
        Strings.Culture.Should().BeNull();
        current.Name.Should().Be(CultureInfo.CurrentUICulture.Name);
    }

    /// <summary>
    /// A remark with a variant of its own finds the text of that variant.
    /// </summary>
    [Fact]
    public void Remark_VariantWithItsOwnText_UsesIt()
    {
        // Act
        var lunch = Strings.Remark(RemarkCode.Encode(PetState.Meal, ScheduleClock.Lunch));
        var dinner = Strings.Remark(RemarkCode.Encode(PetState.Meal, ScheduleClock.Dinner));

        // Assert
        lunch.Should().Contain("Lunch");
        dinner.Should().Contain("Dinner");
    }

    /// <summary>
    /// A remark without a text of its own for the variant falls back to the plain text of the state.
    /// </summary>
    [Fact]
    public void Remark_VariantWithoutText_FallsBackToTheState()
    {
        // Act
        var plain = Strings.Remark(RemarkCode.Encode(PetState.Welcome));
        var variant = Strings.Remark(RemarkCode.Encode(PetState.Welcome, 5));

        // Assert
        plain.Should().NotBeEmpty();
        variant.Should().Be(plain);
    }

    /// <summary>
    /// The chime remark says the hour it was given.
    /// </summary>
    [Fact]
    public void Remark_Chime_SaysTheHour()
    {
        // Act
        var text = Strings.Remark(RemarkCode.Encode(PetState.Chime, 14));

        // Assert
        text.Should().Contain("14");
    }

    /// <summary>
    /// The same lookup follows the interface language.
    /// </summary>
    [Fact]
    public void Remark_Chinese_ReturnsChineseText()
    {
        // Arrange
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

        // Act
        var text = Strings.Remark(RemarkCode.Encode(PetState.Meal, ScheduleClock.Lunch));

        // Assert
        text.Should().Contain("午饭");
    }

    /// <summary>
    /// A state without an explanation has none, a state with one has it.
    /// </summary>
    [Fact]
    public void StateHint_Lookups_BehaveAsDocumented()
    {
        // Assert
        Strings.StateHint(PetState.Idle).Should().BeNull();
        Strings.StateHint(PetState.Building).Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Every scenario has a short name and an explanation in both interface languages, and the name is the shorter one.
    /// </summary>
    /// <param name="culture">
    /// The interface language.
    /// </param>
    [Theory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    public void ScenarioName_EveryScenario_HasShortNameAndExplanation(string culture)
    {
        // Arrange
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

        // Act
        var texts = ScenarioCatalog.All.Select(state => (Name: Strings.ScenarioName(state), Text: Strings.ScenarioLabel(state))).ToList();

        // Assert
        texts.Should().OnlyContain(item => item.Name.Length > 0 && !item.Name.StartsWith('!') && !item.Text.StartsWith('!'));
        texts.Should().OnlyContain(item => item.Name.Length < item.Text.Length);
    }
}
