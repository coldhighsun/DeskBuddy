namespace DeskBuddy.Application.Tests.Brain;

/// <content>
/// Timing of the parts of a tick that read the system.
/// </content>
public partial class PetBrainTests
{
    /// <summary>
    /// Every tick tells how long each of the parts that read the system took, by name.
    /// </summary>
    [Fact]
    public void Tick_ReportsTimeOfEverySensorPart()
    {
        // Arrange
        var timed = new List<string>();
        _brain.SensorTimed = (name, _) => timed.Add(name);

        // Act
        _brain.Tick();

        // Assert
        timed.Should().Equal("idle time", "system load", "full-screen check", "development scenarios", "system scenarios");
    }

    /// <summary>
    /// A brain with nobody listening ticks as before.
    /// </summary>
    [Fact]
    public void Tick_WithoutListener_DoesNotThrow()
    {
        // Arrange
        _brain.SensorTimed = null;

        // Act
        var act = () => _brain.Tick();

        // Assert
        act.Should().NotThrow();
    }
}
