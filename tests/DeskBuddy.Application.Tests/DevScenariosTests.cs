using AwesomeAssertions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.SystemLoad;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="DevScenarios"/>.
/// </summary>
public class DevScenariosTests
{
    /// <summary>
    /// A processor load that keeps a build going.
    /// </summary>
    private const double Busy = 90;

    /// <summary>
    /// A processor load of a machine at rest.
    /// </summary>
    private const double Quiet = 5;

    /// <summary>
    /// The options shared by the policies.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// The clock shared by the policies; local time is UTC.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The fake foreground application.
    /// </summary>
    private readonly FakeForegroundApp _foreground = new();

    /// <summary>
    /// The fake process list.
    /// </summary>
    private readonly FakeProcessList _processes = new();

    /// <summary>
    /// The scenario switches.
    /// </summary>
    private readonly ScenarioSwitches _switches = new();

    /// <summary>
    /// The object under test.
    /// </summary>
    private readonly DevScenarios _scenarios;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevScenariosTests"/> class.
    /// </summary>
    public DevScenariosTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 11, 14, 0, 0, TimeSpan.Zero));
        _scenarios = new DevScenarios(
            _foreground,
            _processes,
            new DevActivityPolicy(_options, _clock) { Processes = PetSettings.DefaultDevProcesses },
            new BuildActivityPolicy(_options, _clock),
            new GitSyncPolicy(_options, _clock),
            new LateNightPolicy(_options, _clock),
            _switches,
            _options);
    }

    /// <summary>
    /// Evaluates once with the given situation, then lets the given time pass.
    /// </summary>
    /// <param name="foreground">
    /// The foreground process name.
    /// </param>
    /// <param name="processes">
    /// The running process names.
    /// </param>
    /// <param name="cpu">
    /// The processor load.
    /// </param>
    /// <param name="seconds">
    /// How long to let pass afterwards.
    /// </param>
    /// <returns>
    /// What was found.
    /// </returns>
    private DevScenarioResult Step(string? foreground, string[] processes, double cpu = Quiet, int seconds = 1)
    {
        _foreground.ProcessName = foreground;
        _processes.Names = new HashSet<string>(processes, StringComparer.OrdinalIgnoreCase);
        var result = _scenarios.Evaluate(TimeSpan.Zero, new SystemLoadSnapshot(cpu, 10));
        _clock.Advance(TimeSpan.FromSeconds(seconds));
        return result;
    }

    /// <summary>
    /// Repeats <see cref="Step"/> once a second.
    /// </summary>
    /// <param name="seconds">
    /// How long to run.
    /// </param>
    /// <param name="foreground">
    /// The foreground process name.
    /// </param>
    /// <param name="processes">
    /// The running process names.
    /// </param>
    /// <param name="cpu">
    /// The processor load.
    /// </param>
    /// <returns>
    /// What was found each time.
    /// </returns>
    private List<DevScenarioResult> Run(int seconds, string? foreground, string[] processes, double cpu = Quiet)
    {
        return [.. Enumerable.Range(0, seconds).Select(_ => Step(foreground, processes, cpu))];
    }

    /// <summary>
    /// A development tool in front means the user is developing, with nothing special going on.
    /// </summary>
    [Fact]
    public void Evaluate_DevelopmentToolInFront_IsDevelopingWithoutAmbient()
    {
        // Act
        var result = Step("Code", []);

        // Assert
        result.IsDeveloping.Should().BeTrue();
        result.Ambient.Should().BeNull();
        result.Moment.Should().BeNull();
    }

    /// <summary>
    /// A build tool with a busy processor makes the pet show the build.
    /// </summary>
    [Fact]
    public void Evaluate_BuildRunning_AmbientIsBuilding()
    {
        // Act
        var results = Run(6, "Code", ["msbuild"], Busy);

        // Assert
        results[^1].Ambient.Should().Be(PetState.Building);
    }

    /// <summary>
    /// A build tool that only idles (a language server) does not.
    /// </summary>
    [Fact]
    public void Evaluate_BuildToolIdling_NoBuilding()
    {
        // Act
        var results = Run(30, "Code", ["dotnet"], Quiet);

        // Assert
        results.Should().OnlyContain(result => result.Ambient == null);
    }

    /// <summary>
    /// When a long build ends the pet acts out the end of it once.
    /// </summary>
    [Fact]
    public void Evaluate_BuildEnds_MomentBuildDone()
    {
        // Arrange
        Run(30, "Code", ["msbuild"], Busy);

        // Act
        var results = Run(10, "Code", [], Quiet);

        // Assert
        var moments = results.Where(result => result.Moment is not null).ToList();
        moments.Should().ContainSingle();
        moments[0].Moment!.Value.State.Should().Be(PetState.BuildDone);
        moments[0].Moment!.Value.Duration.Should().Be(DevScenarios.BuildDoneDuration);
        results[^1].Ambient.Should().BeNull();
    }

    /// <summary>
    /// With the build scenarios switched off nothing about a build is acted out.
    /// </summary>
    [Fact]
    public void Evaluate_BuildScenariosOff_NothingForBuilds()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.Building), nameof(PetState.BuildDone)];

        // Act
        var during = Run(30, "Code", ["msbuild"], Busy);
        var after = Run(10, "Code", [], Quiet);

        // Assert
        during.Concat(after).Should().OnlyContain(result => result.Ambient == null && result.Moment == null);
    }

    /// <summary>
    /// Only the end of the build can be switched off, the loading look can stay.
    /// </summary>
    [Fact]
    public void Evaluate_OnlyBuildDoneOff_StillShowsBuildingButNoCheer()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.BuildDone)];

        // Act
        var during = Run(30, "Code", ["msbuild"], Busy);
        var after = Run(10, "Code", [], Quiet);

        // Assert
        during[^1].Ambient.Should().Be(PetState.Building);
        after.Should().OnlyContain(result => result.Moment == null);
    }

    /// <summary>
    /// With every process-based scenario off the process list is never read.
    /// </summary>
    [Fact]
    public void Evaluate_AllProcessScenariosOff_DoesNotReadProcessList()
    {
        // Arrange
        _switches.Disabled = [nameof(PetState.Building), nameof(PetState.BuildDone), nameof(PetState.GitPush)];

        // Act
        Run(5, "Code", ["msbuild"], Busy);

        // Assert
        _processes.QueryCount.Should().Be(0);
    }

    /// <summary>
    /// With scenarios switched off altogether the foreground-based coding still works.
    /// </summary>
    [Fact]
    public void Evaluate_MasterSwitchOff_NoScenariosButStillDeveloping()
    {
        // Arrange
        _switches.IsEnabled = false;

        // Act
        var results = Run(30, "Code", ["msbuild"], Busy);

        // Assert
        results.Should().OnlyContain(result => result.Ambient == null && result.Moment == null);
        results[^1].IsDeveloping.Should().BeTrue();
    }

    /// <summary>
    /// Git starting to talk to a remote makes the pet ship a parcel, once.
    /// </summary>
    [Fact]
    public void Evaluate_GitRemoteHelperAppears_MomentGitPush()
    {
        // Act
        var first = Step("Code", ["git-remote-https"]);
        var second = Step("Code", ["git-remote-https"]);

        // Assert
        first.Moment!.Value.State.Should().Be(PetState.GitPush);
        first.Moment!.Value.Duration.Should().Be(DevScenarios.GitPushDuration);
        second.Moment.Should().BeNull();
    }

    /// <summary>
    /// Looking things up in a browser right after developing makes the pet read along.
    /// </summary>
    [Fact]
    public void Evaluate_BrowserRightAfterDeveloping_AmbientIsReading()
    {
        // Arrange
        Step("Code", []);

        // Act
        var result = Step("chrome", []);

        // Assert
        result.Ambient.Should().Be(PetState.Reading);
    }

    /// <summary>
    /// A browser the user has been in for a long while is no longer reading up on something.
    /// </summary>
    [Fact]
    public void Evaluate_BrowserForLongAfterDeveloping_NoReading()
    {
        // Arrange
        Step("Code", []);
        Step("chrome", [], seconds: 30);

        // Act
        var result = Step("chrome", [], seconds: 1);
        _clock.Advance(_options.ReadingWindow);
        var later = Step("chrome", []);

        // Assert
        result.Ambient.Should().Be(PetState.Reading);
        later.Ambient.Should().BeNull();
    }

    /// <summary>
    /// A browser the user came to without developing first is not reading up.
    /// </summary>
    [Fact]
    public void Evaluate_BrowserWithoutDevelopingBefore_NoReading()
    {
        // Act
        var result = Step("chrome", []);

        // Assert
        result.Ambient.Should().BeNull();
    }

    /// <summary>
    /// A build running outranks reading up.
    /// </summary>
    [Fact]
    public void Evaluate_BuildAndBrowser_BuildingWins()
    {
        // Arrange
        Run(3, "Code", ["msbuild"], Busy);

        // Act
        var results = Run(6, "chrome", ["msbuild"], Busy);

        // Assert
        results[^1].Ambient.Should().Be(PetState.Building);
    }

    /// <summary>
    /// A long session late at night makes the pet keep the user company, saying so once.
    /// </summary>
    [Fact]
    public void Evaluate_LongSessionAtNight_NightCodingWithOneRemark()
    {
        // Arrange
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 11, 23, 0, 0, TimeSpan.Zero));
        var results = new List<DevScenarioResult>();

        // Act
        for (var minute = 0; minute < 150; minute++)
        {
            results.Add(Step("Code", [], seconds: 60));
        }

        // Assert
        results[10].Ambient.Should().BeNull();
        results[^1].Ambient.Should().Be(PetState.NightCoding);
        results.Count(result => result.Remark == PetState.NightCoding).Should().Be(1);
    }

    /// <summary>
    /// A build in the middle of a late-night session does not make the pet repeat its late-night remark afterwards.
    /// </summary>
    [Fact]
    public void Evaluate_BuildDuringNightSession_NightRemarkNotRepeated()
    {
        // Arrange
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 11, 23, 0, 0, TimeSpan.Zero));
        var results = new List<DevScenarioResult>();
        for (var minute = 0; minute < 150; minute++)
        {
            results.Add(Step("Code", [], seconds: 60));
        }

        // Act
        results.AddRange(Run(20, "Code", ["msbuild"], Busy));
        results.AddRange(Run(30, "Code", [], Quiet));

        // Assert
        results.Should().Contain(result => result.Ambient == PetState.Building);
        results[^1].Ambient.Should().Be(PetState.NightCoding);
        results.Count(result => result.Remark == PetState.NightCoding).Should().Be(1);
    }

    /// <summary>
    /// The same session during the day is just coding.
    /// </summary>
    [Fact]
    public void Evaluate_LongSessionByDay_NoNightCoding()
    {
        // Act
        var results = new List<DevScenarioResult>();
        for (var minute = 0; minute < 150; minute++)
        {
            results.Add(Step("Code", [], seconds: 60));
        }

        // Assert
        results.Should().OnlyContain(result => result.Ambient == null);
    }
}
