using AwesomeAssertions;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Activity;

/// <summary>
/// Tests for <see cref="DevActivityPolicy"/>.
/// </summary>
public class DevActivityPolicyTests
{
    /// <summary>
    /// How recent input must be, in the test options.
    /// </summary>
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the conditions may lapse before the pet gets up, in the test options.
    /// </summary>
    private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The pause that ends a development session.
    /// </summary>
    private static readonly TimeSpan SessionBreak = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The fake clock shared by the policy under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly DevActivityPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevActivityPolicyTests"/> class.
    /// </summary>
    public DevActivityPolicyTests()
    {
        var options = new PetBehaviorOptions { DevActiveWindow = ActiveWindow, DevExitGrace = ExitGrace, DevSessionBreak = SessionBreak };
        _policy = new DevActivityPolicy(options, _clock) { Processes = ["Code", "rider64"] };
    }

    /// <summary>
    /// A development tool in front with recent input counts as developing.
    /// </summary>
    [Fact]
    public void Update_ToolInFrontAndUserActive_IsDeveloping()
    {
        // Act
        var result = _policy.Update("Code", TimeSpan.FromSeconds(2));

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Process names are matched ignoring case.
    /// </summary>
    [Fact]
    public void Update_ProcessNameInOtherCase_IsDeveloping()
    {
        // Act
        var result = _policy.Update("RIDER64", TimeSpan.Zero);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// An application that is not a development tool does not count.
    /// </summary>
    [Fact]
    public void Update_OtherApplication_IsNotDeveloping()
    {
        // Act
        var result = _policy.Update("chrome", TimeSpan.Zero);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// An unknown foreground process does not count.
    /// </summary>
    [Fact]
    public void Update_NoForegroundProcess_IsNotDeveloping()
    {
        // Act
        var result = _policy.Update(null, TimeSpan.Zero);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// A development tool left in front by a user who is away does not count, and there is no grace period for a
    /// session that never started.
    /// </summary>
    [Fact]
    public void Update_ToolInFrontButUserAway_IsNotDeveloping()
    {
        // Act
        var result = _policy.Update("Code", ActiveWindow);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// A short look at another application does not end the session.
    /// </summary>
    [Fact]
    public void Update_BriefSwitchAway_StaysDevelopingDuringGrace()
    {
        // Arrange
        _policy.Update("Code", TimeSpan.Zero);
        _policy.Update("chrome", TimeSpan.Zero);

        // Act
        _clock.Advance(ExitGrace - TimeSpan.FromSeconds(1));
        var result = _policy.Update("chrome", TimeSpan.Zero);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Once the grace period is over the session ends.
    /// </summary>
    [Fact]
    public void Update_SwitchedAwayLongerThanGrace_StopsDeveloping()
    {
        // Arrange
        _policy.Update("Code", TimeSpan.Zero);
        _policy.Update("chrome", TimeSpan.Zero);

        // Act
        _clock.Advance(ExitGrace);
        var result = _policy.Update("chrome", TimeSpan.Zero);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Coming back to the tool within the grace period restarts the grace period for the next time.
    /// </summary>
    [Fact]
    public void Update_ReturnToToolDuringGrace_RestartsGrace()
    {
        // Arrange
        _policy.Update("Code", TimeSpan.Zero);
        _policy.Update("chrome", TimeSpan.Zero);
        _clock.Advance(ExitGrace - TimeSpan.FromSeconds(1));
        _policy.Update("Code", TimeSpan.Zero);
        _policy.Update("chrome", TimeSpan.Zero);

        // Act
        _clock.Advance(ExitGrace - TimeSpan.FromSeconds(1));
        var result = _policy.Update("chrome", TimeSpan.Zero);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// A user who stops typing in the tool keeps the session for the grace period, then loses it.
    /// </summary>
    [Fact]
    public void Update_UserGoesQuiet_EndsAfterGrace()
    {
        // Arrange
        _policy.Update("Code", TimeSpan.Zero);
        _policy.Update("Code", ActiveWindow);

        // Act
        _clock.Advance(ExitGrace - TimeSpan.FromSeconds(1));
        var duringGrace = _policy.Update("Code", ActiveWindow);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var afterGrace = _policy.Update("Code", ActiveWindow);

        // Assert
        duringGrace.Should().BeTrue();
        afterGrace.Should().BeFalse();
    }

    /// <summary>
    /// Lets time pass while feeding the same reading once a second, as the application does.
    /// </summary>
    /// <param name="duration">
    /// How long to run.
    /// </param>
    /// <param name="foreground">
    /// The foreground process name.
    /// </param>
    private void Pass(TimeSpan duration, string foreground)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += TimeSpan.FromSeconds(1))
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            _policy.Update(foreground, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// The length of a session is counted from when development began.
    /// </summary>
    [Fact]
    public void DevelopingFor_AfterSomeWork_IsTheTimeSinceItBegan()
    {
        // Arrange
        _policy.Update("Code", TimeSpan.Zero);

        // Act
        Pass(TimeSpan.FromMinutes(10), "Code");

        // Assert
        _policy.DevelopingFor.Should().Be(TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// A short look at another application does not restart the count.
    /// </summary>
    [Fact]
    public void DevelopingFor_BriefSwitchAway_KeepsCounting()
    {
        // Arrange
        _policy.Update("Code", TimeSpan.Zero);
        Pass(TimeSpan.FromMinutes(10), "Code");
        Pass(TimeSpan.FromSeconds(5), "chrome");
        _policy.Update("Code", TimeSpan.Zero);

        // Assert
        _policy.DevelopingFor.Should().Be(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// While the user is away the count shows zero, but a detour of half a minute does not end the session.
    /// </summary>
    [Fact]
    public void DevelopingFor_AfterADetourPastTheGrace_KeepsTheSession()
    {
        // Arrange
        _policy.Update("Code", TimeSpan.Zero);
        Pass(TimeSpan.FromMinutes(10), "Code");
        Pass(ExitGrace + TimeSpan.FromSeconds(1), "chrome");

        // Act
        var whileAway = _policy.DevelopingFor;
        _policy.Update("Code", TimeSpan.Zero);

        // Assert
        whileAway.Should().Be(TimeSpan.Zero);
        _policy.DevelopingFor.Should().Be(TimeSpan.FromMinutes(10) + ExitGrace + TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// A break of several minutes ends the session, so the count starts over.
    /// </summary>
    [Fact]
    public void DevelopingFor_AfterARealBreak_StartsOver()
    {
        // Arrange
        _policy.Update("Code", TimeSpan.Zero);
        Pass(TimeSpan.FromMinutes(10), "Code");
        Pass(SessionBreak + ExitGrace + TimeSpan.FromSeconds(1), "chrome");

        // Act
        _policy.Update("Code", TimeSpan.Zero);

        // Assert
        _policy.DevelopingFor.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// The time since the user last developed is unknown before they ever did, and counts up afterwards.
    /// </summary>
    [Fact]
    public void SinceDeveloping_IsNullUntilDevelopedThenCountsUp()
    {
        // Arrange
        var before = _policy.SinceDeveloping;
        _policy.Update("Code", TimeSpan.Zero);
        _clock.Advance(ExitGrace);
        _policy.Update("chrome", TimeSpan.Zero);
        _clock.Advance(TimeSpan.FromMinutes(2));
        _policy.Update("chrome", TimeSpan.Zero);

        // Assert
        before.Should().BeNull();
        _policy.SinceDeveloping.Should().Be(TimeSpan.FromMinutes(2));
    }

    /// <summary>
    /// Changing the tool list takes effect on the next update.
    /// </summary>
    [Fact]
    public void Update_ProcessesChanged_UsesNewList()
    {
        // Arrange
        _policy.Processes = ["notepad++"];

        // Act
        var code = _policy.Update("Code", TimeSpan.Zero);
        var notepad = _policy.Update("notepad++", TimeSpan.Zero);

        // Assert
        code.Should().BeFalse();
        notepad.Should().BeTrue();
    }
}
