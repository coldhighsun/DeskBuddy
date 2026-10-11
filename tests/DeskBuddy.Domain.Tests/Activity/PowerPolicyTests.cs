using AwesomeAssertions;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Power;

namespace DeskBuddy.Domain.Tests.Activity;

/// <summary>
/// Tests for <see cref="PowerPolicy"/>.
/// </summary>
public class PowerPolicyTests
{
    /// <summary>
    /// The options under test: low at 20 percent, recovered at 25.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly PowerPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerPolicyTests"/> class.
    /// </summary>
    public PowerPolicyTests()
    {
        _policy = new PowerPolicy(_options);
    }

    /// <summary>
    /// Builds a status of a computer with a battery.
    /// </summary>
    /// <param name="percent">
    /// The charge.
    /// </param>
    /// <param name="pluggedIn">
    /// Whether the computer is on mains power.
    /// </param>
    /// <returns>
    /// The status.
    /// </returns>
    private static PowerStatus Battery(int percent, bool pluggedIn = false)
    {
        return new PowerStatus(true, percent, pluggedIn);
    }

    /// <summary>
    /// After a reset the cable that was plugged in meanwhile does not count as plugged in just now.
    /// </summary>
    [Fact]
    public void Reset_ThenPluggedIn_IsNotJustPluggedIn()
    {
        // Arrange
        _policy.Update(Battery(50));

        // Act
        _policy.Reset();
        var change = _policy.Update(Battery(52, pluggedIn: true));

        // Assert
        change.JustPluggedIn.Should().BeFalse();
    }

    /// <summary>
    /// A battery well above the limit is not low.
    /// </summary>
    [Fact]
    public void Update_PlentyOfCharge_IsNotLow()
    {
        // Act
        var change = _policy.Update(Battery(80));

        // Assert
        change.IsLow.Should().BeFalse();
    }

    /// <summary>
    /// A battery at the limit, running on battery, is low.
    /// </summary>
    [Fact]
    public void Update_AtLimitOnBattery_IsLow()
    {
        // Act
        var change = _policy.Update(Battery(_options.LowBatteryPercent));

        // Assert
        change.IsLow.Should().BeTrue();
    }

    /// <summary>
    /// A low charge does not count while the computer is plugged in.
    /// </summary>
    [Fact]
    public void Update_LowButPluggedIn_IsNotLow()
    {
        // Act
        var change = _policy.Update(Battery(10, pluggedIn: true));

        // Assert
        change.IsLow.Should().BeFalse();
    }

    /// <summary>
    /// A charge hovering just above the limit after it was low does not make the pet flicker.
    /// </summary>
    [Fact]
    public void Update_HoversJustAboveLimit_StaysLow()
    {
        // Arrange
        _policy.Update(Battery(_options.LowBatteryPercent));

        // Act
        var change = _policy.Update(Battery(_options.LowBatteryPercent + 2));

        // Assert
        change.IsLow.Should().BeTrue();
    }

    /// <summary>
    /// Plugging in ends the low state and is reported once.
    /// </summary>
    [Fact]
    public void Update_PluggedInWhileLow_ReportsOnceAndEndsLow()
    {
        // Arrange
        _policy.Update(Battery(15));

        // Act
        var first = _policy.Update(Battery(15, pluggedIn: true));
        var second = _policy.Update(Battery(16, pluggedIn: true));

        // Assert
        first.JustPluggedIn.Should().BeTrue();
        first.IsLow.Should().BeFalse();
        second.JustPluggedIn.Should().BeFalse();
    }

    /// <summary>
    /// A cable that was already plugged in when the application started is not news.
    /// </summary>
    [Fact]
    public void Update_FirstReadingPluggedIn_IsNotJustPluggedIn()
    {
        // Act
        var change = _policy.Update(Battery(50, pluggedIn: true));

        // Assert
        change.JustPluggedIn.Should().BeFalse();
    }

    /// <summary>
    /// A computer without a battery never reports anything.
    /// </summary>
    [Fact]
    public void Update_NoBattery_ReportsNothing()
    {
        // Act
        var first = _policy.Update(new PowerStatus(false, null, true));
        var second = _policy.Update(new PowerStatus(false, null, false));

        // Assert
        first.Should().Be(default(PowerChange));
        second.Should().Be(default(PowerChange));
    }

    /// <summary>
    /// An unknown charge is not taken for a low one.
    /// </summary>
    [Fact]
    public void Update_UnknownCharge_IsNotLow()
    {
        // Act
        var change = _policy.Update(new PowerStatus(true, null, false));

        // Assert
        change.IsLow.Should().BeFalse();
    }
}
