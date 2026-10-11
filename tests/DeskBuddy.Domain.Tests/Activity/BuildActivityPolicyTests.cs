using AwesomeAssertions;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Activity;

/// <summary>
/// Tests for <see cref="BuildActivityPolicy"/>.
/// </summary>
public class BuildActivityPolicyTests
{
    /// <summary>
    /// A load that starts a build.
    /// </summary>
    private const double Busy = 90;

    /// <summary>
    /// A load too low even to keep a build going.
    /// </summary>
    private const double Quiet = 5;

    /// <summary>
    /// The options under test.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// The fake clock shared by the policy under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly BuildActivityPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildActivityPolicyTests"/> class.
    /// </summary>
    public BuildActivityPolicyTests()
    {
        _policy = new BuildActivityPolicy(_options, _clock);
    }

    /// <summary>
    /// After a reset a build that was running is forgotten, so a later reading does not report it as finished.
    /// </summary>
    [Fact]
    public void Reset_WhileBuilding_ForgetsTheBuild()
    {
        // Arrange
        Run(20, toolRunning: true, cpu: Busy);
        _policy.IsBuilding.Should().BeTrue();

        // Act
        _policy.Reset();
        _clock.Advance(TimeSpan.FromHours(2));
        var changes = Run(10, toolRunning: false, cpu: Quiet);

        // Assert
        _policy.IsBuilding.Should().BeFalse();
        changes.Should().OnlyContain(change => change == BuildChange.None);
    }

    /// <summary>
    /// Lets time pass while feeding the same reading once a second.
    /// </summary>
    /// <param name="seconds">
    /// How long to run.
    /// </param>
    /// <param name="toolRunning">
    /// Whether a build tool is running.
    /// </param>
    /// <param name="cpu">
    /// The processor load.
    /// </param>
    /// <returns>
    /// Every change that was reported.
    /// </returns>
    private List<BuildChange> Run(int seconds, bool toolRunning, double cpu)
    {
        var changes = new List<BuildChange>();
        for (var second = 0; second < seconds; second++)
        {
            changes.Add(_policy.Update(toolRunning, cpu));
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        return changes;
    }

    /// <summary>
    /// A build tool with a busy processor, held for a few seconds, is a build.
    /// </summary>
    [Fact]
    public void Update_ToolAndBusyProcessorForAWhile_StartsBuild()
    {
        // Act
        var changes = Run(5, toolRunning: true, Busy);

        // Assert
        changes.Should().Contain(BuildChange.Started);
        _policy.IsBuilding.Should().BeTrue();
    }

    /// <summary>
    /// A brief spike is not a build.
    /// </summary>
    [Fact]
    public void Update_BriefSpike_IsNotABuild()
    {
        // Act
        Run(2, toolRunning: true, Busy);
        Run(5, toolRunning: true, Quiet);

        // Assert
        _policy.IsBuilding.Should().BeFalse();
    }

    /// <summary>
    /// A build tool that is merely sitting there (a language server) is not a build, however long it runs.
    /// </summary>
    [Fact]
    public void Update_ToolWithQuietProcessor_IsNotABuild()
    {
        // Act
        var changes = Run(60, toolRunning: true, Quiet);

        // Assert
        changes.Should().OnlyContain(change => change == BuildChange.None);
        _policy.IsBuilding.Should().BeFalse();
    }

    /// <summary>
    /// A busy processor without any build tool is not a build.
    /// </summary>
    [Fact]
    public void Update_BusyProcessorWithoutTool_IsNotABuild()
    {
        // Act
        Run(60, toolRunning: false, Busy);

        // Assert
        _policy.IsBuilding.Should().BeFalse();
    }

    /// <summary>
    /// A build that has gone on long enough is reported as finished once the tools are gone for the grace period.
    /// </summary>
    [Fact]
    public void Update_LongBuildEnds_ReportsFinishedOnce()
    {
        // Arrange
        Run(30, toolRunning: true, Busy);

        // Act
        var changes = Run(10, toolRunning: false, Quiet);

        // Assert
        changes.Count(change => change == BuildChange.Finished).Should().Be(1);
        _policy.IsBuilding.Should().BeFalse();
    }

    /// <summary>
    /// A pause shorter than the grace period does not end the build.
    /// </summary>
    [Fact]
    public void Update_BriefPause_DoesNotEndBuild()
    {
        // Arrange
        Run(30, toolRunning: true, Busy);

        // Act
        var changes = Run(2, toolRunning: true, Quiet);
        changes.AddRange(Run(10, toolRunning: true, Busy));

        // Assert
        changes.Should().NotContain(BuildChange.Finished);
        _policy.IsBuilding.Should().BeTrue();
    }

    /// <summary>
    /// A build too short to be worth a cheer ends without a report.
    /// </summary>
    [Fact]
    public void Update_ShortBuildEnds_ReportsNothing()
    {
        // Arrange
        Run(5, toolRunning: true, Busy);

        // Act
        var changes = Run(10, toolRunning: false, Quiet);

        // Assert
        changes.Should().NotContain(BuildChange.Finished);
        _policy.IsBuilding.Should().BeFalse();
    }

    /// <summary>
    /// After one build ends, the next one is recognized again.
    /// </summary>
    [Fact]
    public void Update_SecondBuild_IsRecognized()
    {
        // Arrange
        Run(30, toolRunning: true, Busy);
        Run(10, toolRunning: false, Quiet);

        // Act
        var changes = Run(5, toolRunning: true, Busy);

        // Assert
        changes.Should().Contain(BuildChange.Started);
    }
}
