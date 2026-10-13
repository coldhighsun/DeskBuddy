using AwesomeAssertions;
using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="HighContrastReader"/>.
/// </summary>
public class HighContrastReaderTests
{
    /// <summary>
    /// The system answers the high-contrast question. A wrong structure size or layout would make the call fail, which
    /// <see cref="HighContrastReader.IsOn"/> would silently report as high contrast being off.
    /// </summary>
    [Fact]
    public void TryRead_OnThisMachine_GetsAnAnswerFromTheSystem()
    {
        // Act
        var answered = HighContrastReader.TryRead(out var on);

        // Assert
        answered.Should().BeTrue("the structure passed to SystemParametersInfo must be accepted by Windows");
        HighContrastReader.IsOn().Should().Be(on);
    }
}
