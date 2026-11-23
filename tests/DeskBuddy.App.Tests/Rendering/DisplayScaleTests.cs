using DeskBuddy.App.Rendering;

namespace DeskBuddy.App.Tests.Rendering;

/// <summary>
/// Tests for <see cref="DisplayScale"/>.
/// </summary>
public sealed class DisplayScaleTests
{
    /// <summary>
    /// The monitor with the largest scaling decides, as the pet window may be dragged onto any of them.
    /// </summary>
    [Fact]
    public void Largest_SeveralMonitors_ReturnsTheLargest()
    {
        // Act
        var scale = DisplayScale.Largest([1.0, 1.25, 2.0, 1.5]);

        // Assert
        scale.Should().Be(2.0);
    }

    /// <summary>
    /// Without a monitor to ask (they cannot be listed) 100 % is taken.
    /// </summary>
    [Fact]
    public void Largest_NoMonitors_Returns100Percent()
    {
        // Act
        var scale = DisplayScale.Largest([]);

        // Assert
        scale.Should().Be(1.0);
    }

    /// <summary>
    /// A scaling that is out of range or no number does not count beyond the range.
    /// </summary>
    /// <param name="reported">The scaling a monitor reports.</param>
    /// <param name="expected">The scaling that is used.</param>
    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(-1.0, 1.0)]
    [InlineData(7.0, 3.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 3.0)]
    public void Largest_ScalingOutOfRange_IsKeptInRange(double reported, double expected)
    {
        // Act
        var scale = DisplayScale.Largest([reported]);

        // Assert
        scale.Should().Be(expected);
    }

    /// <summary>
    /// The real monitors give a scaling inside the range, whatever the machine has.
    /// </summary>
    [Fact]
    public void Current_ThisMachine_IsInsideTheRange()
    {
        // Act
        var scale = DisplayScale.Current();

        // Assert
        scale.Should().BeInRange(DisplayScale.Minimum, DisplayScale.Maximum);
    }
}
