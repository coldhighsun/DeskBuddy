using DeskBuddy.Infrastructure.Sensors;

namespace DeskBuddy.Infrastructure.Tests.Sensors;

/// <summary>
/// Tests for <see cref="WindowsPowerStatusProvider"/>, against the real power supply of the machine.
/// </summary>
public class WindowsPowerStatusProviderTests
{
    /// <summary>
    /// The reading is consistent whatever the machine: a charge is only reported for a computer with a battery, and
    /// lies between 0 and 100.
    /// </summary>
    [Fact]
    public void GetStatus_ReturnsAConsistentReading()
    {
        // Act
        var status = new WindowsPowerStatusProvider().GetStatus();

        // Assert
        if (status.BatteryPercent is { } percent)
        {
            percent.Should().BeInRange(0, 100);
        }

        if (!status.HasBattery)
        {
            status.BatteryPercent.Should().BeNull();
        }
    }
}
