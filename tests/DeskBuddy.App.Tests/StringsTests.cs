using System.Globalization;
using AwesomeAssertions;
using DeskBuddy.App.Resources;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Schedule;

namespace DeskBuddy.App.Tests;

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
}
