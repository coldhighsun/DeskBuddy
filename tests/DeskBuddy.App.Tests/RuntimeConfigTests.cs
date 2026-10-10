using System.IO;
using System.Text.Json;
using AwesomeAssertions;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Checks the runtime settings the application is built with.
/// </summary>
public sealed class RuntimeConfigTests
{
    /// <summary>
    /// Tiered compilation stays off: with it on the pet's timers keep the runtime re-compiling code for minutes, which
    /// measured as several times the CPU use. If a change turns it back on, this test fails and the measurement in
    /// <c>tools/Measure-Cpu.ps1</c> should be repeated first.
    /// </summary>
    [Fact]
    public void RuntimeConfig_TieredCompilation_IsDisabled()
    {
        // Arrange
        var path = Path.Combine(AppContext.BaseDirectory, "DeskBuddy.App.runtimeconfig.json");

        // Act
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var properties = document.RootElement.GetProperty("runtimeOptions").GetProperty("configProperties");

        // Assert
        properties.GetProperty("System.Runtime.TieredCompilation").GetBoolean().Should().BeFalse();
    }
}
