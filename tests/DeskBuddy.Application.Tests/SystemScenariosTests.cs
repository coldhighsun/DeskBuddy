using AwesomeAssertions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Settings;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="SystemScenarios"/>.
/// </summary>
public class SystemScenariosTests
{
    /// <summary>
    /// The options shared by the policies.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// The fake foreground application.
    /// </summary>
    private readonly FakeForegroundApp _foreground = new();

    /// <summary>
    /// The fake power supply.
    /// </summary>
    private readonly FakePowerStatusProvider _power = new();

    /// <summary>
    /// The scenario switches.
    /// </summary>
    private readonly ScenarioSwitches _switches = new();

    /// <summary>
    /// The scenario switches of the clock; off until a test turns them on.
    /// </summary>
    private readonly ScenarioSwitches _clockSwitches = new() { IsEnabled = false };

    /// <summary>
    /// The fake time source, running in the UTC zone, on a Wednesday at nine in the morning.
    /// </summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 11, 9, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// The object under test.
    /// </summary>
    private readonly SystemScenarios _scenarios;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemScenariosTests"/> class.
    /// </summary>
    public SystemScenariosTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _scenarios = new SystemScenarios(
            _foreground,
            _power,
            new MeetingPolicy { Processes = PetSettings.DefaultMeetingProcesses },
            new ChatPolicy { Processes = PetSettings.DefaultChatProcesses },
            new WelcomeBackPolicy(_options),
            new PowerPolicy(_options),
            new ScheduleClock(_options, _time, _clockSwitches),
            _switches);
    }

    /// <summary>
    /// A meeting application in front makes the pet keep company in a headset.
    /// </summary>
    [Fact]
    public void Evaluate_MeetingApplicationInFront_AmbientIsMeeting()
    {
        // Arrange
        _foreground.ProcessName = "Zoom";

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Ambient.Should().Be(PetState.Meeting);
    }

    /// <summary>
    /// Any other application is not a meeting.
    /// </summary>
    [Fact]
    public void Evaluate_OtherApplication_NoAmbient()
    {
        // Arrange
        _foreground.ProcessName = "Code";

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Ambient.Should().BeNull();
    }

    /// <summary>
    /// With the meeting and chatting scenarios off the foreground window is not even looked at.
    /// </summary>
    [Fact]
    public void Evaluate_MeetingAndChattingOff_DoesNotLookAtForegroundWindow()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.Meeting), nameof(PetState.Chatting)];
        _foreground.ProcessName = "Zoom";

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Ambient.Should().BeNull();
        _foreground.QueryCount.Should().Be(0);
    }

    /// <summary>
    /// A chat application in front makes the pet chat along.
    /// </summary>
    [Fact]
    public void Evaluate_ChatApplicationInFront_AmbientIsChatting()
    {
        // Arrange
        _foreground.ProcessName = "Weixin";

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Ambient.Should().Be(PetState.Chatting);
    }

    /// <summary>
    /// With the chatting scenario off a chat application in front changes nothing.
    /// </summary>
    [Fact]
    public void Evaluate_ChattingOff_NoAmbient()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.Chatting)];
        _foreground.ProcessName = "Weixin";

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Ambient.Should().BeNull();
    }

    /// <summary>
    /// A process that is both a meeting and a chat application counts as the meeting, which ranks higher.
    /// </summary>
    [Fact]
    public void Evaluate_MeetingAndChatBoth_MeetingWins()
    {
        // Arrange
        var scenarios = new SystemScenarios(
            _foreground,
            _power,
            new MeetingPolicy { Processes = ["Weixin"] },
            new ChatPolicy { Processes = ["Weixin"] },
            new WelcomeBackPolicy(_options),
            new PowerPolicy(_options),
            new ScheduleClock(_options, _time, _clockSwitches),
            _switches);
        _foreground.ProcessName = "Weixin";

        // Act
        var result = scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Ambient.Should().Be(PetState.Meeting);
    }

    /// <summary>
    /// Coming back after a long absence makes the pet wave hello and say so.
    /// </summary>
    [Fact]
    public void Evaluate_BackAfterLongAbsence_MomentWelcomeWithRemark()
    {
        // Arrange
        _scenarios.Evaluate(_options.WelcomeAwayAfter + TimeSpan.FromMinutes(5));

        // Act
        var result = _scenarios.Evaluate(TimeSpan.FromSeconds(1));

        // Assert
        result.Moment!.Value.State.Should().Be(PetState.Welcome);
        result.Moment!.Value.Duration.Should().Be(SystemScenarios.WelcomeDuration);
        result.Remark.Should().Be(PetState.Welcome);
    }

    /// <summary>
    /// With the welcome off nothing is done when the user returns.
    /// </summary>
    [Fact]
    public void Evaluate_WelcomeOff_NoMoment()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.Welcome)];
        _scenarios.Evaluate(_options.WelcomeAwayAfter + TimeSpan.FromMinutes(5));

        // Act
        var result = _scenarios.Evaluate(TimeSpan.FromSeconds(1));

        // Assert
        result.Moment.Should().BeNull();
    }

    /// <summary>
    /// A low battery on battery power is shown, with one remark when it begins.
    /// </summary>
    [Fact]
    public void Evaluate_BatteryLow_AmbientLowBatteryWithOneRemark()
    {
        // Arrange
        _power.Status = new PowerStatus(true, 12, false);

        // Act
        var first = _scenarios.Evaluate(TimeSpan.Zero);
        var second = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        first.Ambient.Should().Be(PetState.LowBattery);
        first.Remark.Should().Be(PetState.LowBattery);
        second.Ambient.Should().Be(PetState.LowBattery);
        second.Remark.Should().BeNull();
    }

    /// <summary>
    /// A meeting that ends while the battery is still low does not make the pet say the low-battery remark again.
    /// </summary>
    [Fact]
    public void Evaluate_MeetingEndsWithBatteryStillLow_NoSecondRemark()
    {
        // Arrange
        _power.Status = new PowerStatus(true, 12, false);
        var first = _scenarios.Evaluate(TimeSpan.Zero);
        _foreground.ProcessName = "Zoom";
        var inMeeting = _scenarios.Evaluate(TimeSpan.Zero);

        // Act
        _foreground.ProcessName = "Code";
        var after = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        first.Remark.Should().Be(PetState.LowBattery);
        inMeeting.Ambient.Should().Be(PetState.Meeting);
        after.Ambient.Should().Be(PetState.LowBattery);
        after.Remark.Should().BeNull();
    }

    /// <summary>
    /// Plugging the cable in makes the pet charge up and ends the low battery.
    /// </summary>
    [Fact]
    public void Evaluate_PluggedIn_MomentChargingAndNoLowBattery()
    {
        // Arrange
        _power.Status = new PowerStatus(true, 12, false);
        _scenarios.Evaluate(TimeSpan.Zero);
        _power.Status = new PowerStatus(true, 12, true);

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Moment!.Value.State.Should().Be(PetState.Charging);
        result.Moment!.Value.Duration.Should().Be(SystemScenarios.ChargingDuration);
        result.Ambient.Should().BeNull();
    }

    /// <summary>
    /// A meeting outranks a low battery.
    /// </summary>
    [Fact]
    public void Evaluate_MeetingAndLowBattery_MeetingWins()
    {
        // Arrange
        _foreground.ProcessName = "Zoom";
        _power.Status = new PowerStatus(true, 12, false);

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Ambient.Should().Be(PetState.Meeting);
    }

    /// <summary>
    /// With both battery scenarios off the power supply is never read.
    /// </summary>
    [Fact]
    public void Evaluate_BatteryScenariosOff_DoesNotReadPowerStatus()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.LowBattery), nameof(PetState.Charging)];

        // Act
        _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        _power.QueryCount.Should().Be(0);
    }

    /// <summary>
    /// A user who was away while Welcome was switched off is not welcomed when it is switched on.
    /// </summary>
    [Fact]
    public void Evaluate_WelcomeSwitchedOffWhileAway_DoesNotWelcomeAfterSwitchingOn()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.Welcome)];
        _scenarios.Evaluate(TimeSpan.FromHours(1));
        _switches.Disabled = [];

        // Act
        var result = _scenarios.Evaluate(TimeSpan.FromSeconds(1));

        // Assert
        result.Moment.Should().BeNull();
    }

    /// <summary>
    /// Lunch time makes the pet hold up a bowl, with a remark about lunch.
    /// </summary>
    [Fact]
    public void Evaluate_LunchTime_MomentIsMealWithLunchRemark()
    {
        // Arrange
        _clockSwitches.IsEnabled = true;
        _time.SetUtcNow(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Moment.Should().Be((PetState.Meal, SystemScenarios.MealDuration));
        result.Remark.Should().Be(PetState.Meal);
        result.RemarkVariant.Should().Be(ScheduleClock.Lunch);
    }

    /// <summary>
    /// A user who has been away for a while is not there to see the clock.
    /// </summary>
    [Fact]
    public void Evaluate_LunchTimeButUserAway_NoMomentAndOccasionKept()
    {
        // Arrange
        _clockSwitches.IsEnabled = true;
        _time.SetUtcNow(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));

        // Act
        var away = _scenarios.Evaluate(TimeSpan.FromMinutes(10));
        var back = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        away.Moment.Should().BeNull();
        back.Moment.Should().NotBeNull();
    }

    /// <summary>
    /// While the pet must not disturb, the occasion is held back, not lost.
    /// </summary>
    [Fact]
    public void Evaluate_LunchTimeButCannotDisturb_NoMomentAndOccasionKept()
    {
        // Arrange
        _clockSwitches.IsEnabled = true;
        _time.SetUtcNow(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));

        // Act
        var held = _scenarios.Evaluate(TimeSpan.Zero, canDisturb: false);
        var later = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        held.Moment.Should().BeNull();
        later.Moment.Should().NotBeNull();
    }

    /// <summary>
    /// A meeting in front keeps the clock quiet.
    /// </summary>
    [Fact]
    public void Evaluate_LunchTimeInMeeting_NoMoment()
    {
        // Arrange
        _clockSwitches.IsEnabled = true;
        _foreground.ProcessName = "Zoom";
        _time.SetUtcNow(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Moment.Should().BeNull();
    }

    /// <summary>
    /// Welcoming the user back takes precedence over the clock, which waits for its turn.
    /// </summary>
    [Fact]
    public void Evaluate_WelcomeAndLunchTogether_WelcomeFirstThenMeal()
    {
        // Arrange
        _clockSwitches.IsEnabled = true;
        _time.SetUtcNow(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));
        _scenarios.Evaluate(TimeSpan.FromMinutes(40), canDisturb: false);

        // Act
        var result = _scenarios.Evaluate(TimeSpan.Zero);
        _time.Advance(TimeSpan.FromSeconds(5));
        var next = _scenarios.Evaluate(TimeSpan.Zero);

        // Assert
        result.Moment.Should().Be((PetState.Welcome, SystemScenarios.WelcomeDuration));
        next.Moment.Should().Be((PetState.Meal, SystemScenarios.MealDuration));
    }
}
