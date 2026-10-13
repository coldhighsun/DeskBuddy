using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AwesomeAssertions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for the language resource files: the Simplified Chinese <c>Strings.resx</c> and the English
/// <c>Strings.en.resx</c> must say the same things.
/// </summary>
public partial class ResourceFilesTests
{
    /// <summary>
    /// The Simplified Chinese texts by key.
    /// </summary>
    private readonly Dictionary<string, string> _chinese = Read("Strings.resx");

    /// <summary>
    /// The English texts by key.
    /// </summary>
    private readonly Dictionary<string, string> _english = Read("Strings.en.resx");

    /// <summary>
    /// Every text exists in both languages.
    /// </summary>
    [Fact]
    public void Resources_BothLanguages_HaveTheSameKeys()
    {
        // Assert
        _english.Keys.Should().BeEquivalentTo(_chinese.Keys);
    }

    /// <summary>
    /// No text is empty in either language.
    /// </summary>
    [Fact]
    public void Resources_NoEmptyTexts()
    {
        // Assert
        _chinese.Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key).Should().BeEmpty();
        _english.Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key).Should().BeEmpty();
    }

    /// <summary>
    /// A text that is filled in with values takes the same values in both languages.
    /// </summary>
    [Fact]
    public void Resources_Placeholders_MatchBetweenLanguages()
    {
        // Act
        var mismatches = _chinese.Keys
            .Where(key => _english.ContainsKey(key))
            .Where(key => !Placeholders(_chinese[key]).SequenceEqual(Placeholders(_english[key])))
            .ToList();

        // Assert
        mismatches.Should().BeEmpty();
    }

    /// <summary>
    /// Every scenario has a label for the settings and an explanation for when the mouse rests on the pet.
    /// </summary>
    [Fact]
    public void Resources_EveryScenario_HasLabelAndHint()
    {
        // Act
        var keys = ScenarioCatalog.All.SelectMany(state => new[] { $"Scenario{state}", $"StateHint{state}" });

        // Assert
        keys.Where(key => !_chinese.ContainsKey(key)).Should().BeEmpty();
    }

    /// <summary>
    /// Every state the pet previews show has a name in both languages.
    /// </summary>
    [Fact]
    public void Resources_EveryShowcaseState_HasName()
    {
        // Act
        var keys = PetShowcase.States.Select(state => $"PetStateName{state}").ToList();

        // Assert
        keys.Where(key => !_chinese.ContainsKey(key) || !_english.ContainsKey(key)).Should().BeEmpty();
    }

    /// <summary>
    /// The pet at its computer has an explanation too.
    /// </summary>
    [Fact]
    public void Resources_Coding_HasHint()
    {
        // Assert
        _chinese.Should().ContainKey($"StateHint{PetState.Coding}");
    }

    /// <summary>
    /// Every casual remark the pet can make has a text.
    /// </summary>
    [Fact]
    public void Resources_EveryChatterRemark_HasText()
    {
        // Act
        var keys = Enumerable.Range(0, ChatterPolicy.VariantCount).Select(index => $"Chatter{index}");

        // Assert
        keys.Where(key => !_chinese.ContainsKey(key)).Should().BeEmpty();
    }

    /// <summary>
    /// Reads a resource file of the application project.
    /// </summary>
    /// <param name="fileName">
    /// The file name inside the project's <c>Resources</c> folder.
    /// </param>
    /// <returns>
    /// The texts by key.
    /// </returns>
    private static Dictionary<string, string> Read(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "DeskBuddy.App", "Resources", fileName);
            if (File.Exists(path))
            {
                return XDocument.Load(path).Root!.Elements("data").ToDictionary(
                    element => (string)element.Attribute("name")!,
                    element => (string?)element.Element("value") ?? string.Empty);
            }
        }

        throw new FileNotFoundException($"{fileName} was not found above the test output.");
    }

    /// <summary>
    /// Lists the placeholders such as <c>{0}</c> of a text, in order of their numbers.
    /// </summary>
    /// <param name="text">
    /// The text.
    /// </param>
    /// <returns>
    /// The placeholder numbers, without duplicates.
    /// </returns>
    private static IEnumerable<int> Placeholders(string text)
    {
        return PlaceholderPattern().Matches(text).Select(match => int.Parse(match.Groups[1].Value)).Distinct().Order();
    }

    /// <summary>
    /// Matches a placeholder.
    /// </summary>
    /// <returns>
    /// The pattern.
    /// </returns>
    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex PlaceholderPattern();
}
