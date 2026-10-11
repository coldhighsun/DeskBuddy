using AwesomeAssertions;
using DeskBuddy.Infrastructure.Sensors;

namespace DeskBuddy.Infrastructure.Tests.Sensors;

/// <summary>
/// Tests for <see cref="WindowsProcessList"/>, against the real process list of the machine.
/// </summary>
public class WindowsProcessListTests
{
    /// <summary>
    /// The list contains the process running the test, by name without extension and ignoring case.
    /// </summary>
    [Fact]
    public void GetNames_ContainsTheCurrentProcess()
    {
        // Arrange
        var current = Path.GetFileNameWithoutExtension(Environment.ProcessPath)!;

        // Act
        var names = new WindowsProcessList().GetNames();

        // Assert
        names.Should().NotBeEmpty();
        names.Contains(current).Should().BeTrue();
        names.Contains(current.ToUpperInvariant()).Should().BeTrue();
    }

    /// <summary>
    /// Names carry no <c>.exe</c> extension.
    /// </summary>
    [Fact]
    public void GetNames_NamesHaveNoExtension()
    {
        // Act
        var names = new WindowsProcessList().GetNames();

        // Assert
        names.Should().NotContain(name => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }
}
