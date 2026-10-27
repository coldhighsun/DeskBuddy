using System.IO;
using System.Runtime.CompilerServices;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests that the installer script and the update flow agree on how a silent update restarts the application.
/// </summary>
public sealed class InstallerScriptTests
{
    /// <summary>
    /// The installer starts the application after a silent run only when it is given the switch the updater passes.
    /// </summary>
    [Fact]
    public void Script_RelaunchSwitch_MatchesWhatTheUpdaterPasses()
    {
        // Arrange
        var script = File.ReadAllText(ScriptPath());
        var switchName = AppUpdates.RelaunchArgument.TrimStart('/').Split('=')[0];

        // Act
        var readsSwitch = script.Contains($"{{param:{switchName}|", StringComparison.Ordinal);

        // Assert
        readsSwitch.Should().BeTrue();
        script.Should().Contain("Check: ShouldLaunchApp");
        script.Should().NotContain("skipifsilent");
    }

    /// <summary>
    /// Finds the installer script in the repository this test file is part of.
    /// </summary>
    /// <param name="testFile">
    /// Filled in by the compiler with the path of this file.
    /// </param>
    /// <returns>
    /// The path of <c>installer/DeskBuddy.iss</c>.
    /// </returns>
    private static string ScriptPath([CallerFilePath] string testFile = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", "installer", "DeskBuddy.iss"));
    }
}
