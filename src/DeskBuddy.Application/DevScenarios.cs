using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.SystemLoad;

namespace DeskBuddy.Application;

/// <summary>
/// What <see cref="DevScenarios.Evaluate"/> found for one moment.
/// </summary>
/// <param name="IsDeveloping">
/// Whether the user is working in a development tool.
/// </param>
/// <param name="Ambient">
/// The state the pet should hold for as long as it applies (a build, reading up on something, a late night), or
/// <see langword="null"/>.
/// </param>
/// <param name="Moment">
/// A one-off state the pet should act out right now, with how long, or <see langword="null"/>.
/// </param>
/// <param name="Remark">
/// The state whose short remark should be said right now, or <see langword="null"/>.
/// </param>
public readonly record struct DevScenarioResult(
    bool IsDeveloping,
    PetState? Ambient,
    (PetState State, TimeSpan Duration)? Moment,
    PetState? Remark);

/// <summary>
/// Everything the pet does because of what the user is doing as a developer: sitting down at its computer, a build
/// running or finishing, git talking to a remote, a long night, a look things up in the browser. It reads the front
/// application and the process list, but only names.
/// </summary>
/// <param name="foregroundApp">
/// Tells which application is in front.
/// </param>
/// <param name="processes">
/// Tells which processes are running.
/// </param>
/// <param name="devActivity">
/// Decides whether the user is developing, and for how long.
/// </param>
/// <param name="build">
/// Recognizes builds.
/// </param>
/// <param name="git">
/// Notices git talking to a remote.
/// </param>
/// <param name="lateNight">
/// Decides whether it is a late-night session.
/// </param>
/// <param name="switches">
/// Which scenarios the user has switched on.
/// </param>
/// <param name="options">
/// The timings.
/// </param>
public sealed class DevScenarios(
    IForegroundAppDetector foregroundApp,
    IProcessList processes,
    DevActivityPolicy devActivity,
    BuildActivityPolicy build,
    GitSyncPolicy git,
    LateNightPolicy lateNight,
    ScenarioSwitches switches,
    PetBehaviorOptions options)
{
    /// <summary>
    /// How long the pet acts out the end of a build.
    /// </summary>
    public static readonly TimeSpan BuildDoneDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the pet acts out shipping a parcel for git traffic.
    /// </summary>
    public static readonly TimeSpan GitPushDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Whether the late-night session applied at the previous evaluation, to say its remark only when it begins, not
    /// each time a build or a look in the browser stops hiding it.
    /// </summary>
    private bool _wasNightCoding;

    /// <summary>
    /// Reads the sensors and works out what the pet should do about them.
    /// </summary>
    /// <param name="userIdle">
    /// How long the user has had no keyboard or mouse input.
    /// </param>
    /// <param name="load">
    /// The latest load reading.
    /// </param>
    /// <returns>
    /// What the pet should do.
    /// </returns>
    public DevScenarioResult Evaluate(TimeSpan userIdle, SystemLoadSnapshot load)
    {
        var foreground = foregroundApp.GetForegroundProcessName();
        var isDeveloping = devActivity.Update(foreground, userIdle);

        var wantsBuild = switches.Allows(PetState.Building) || switches.Allows(PetState.BuildDone);
        var wantsGit = switches.Allows(PetState.GitPush);
        var names = wantsBuild || wantsGit ? processes.GetNames() : null;

        (PetState State, TimeSpan Duration)? moment = null;
        var active = new List<PetState>(3);

        if (!wantsBuild)
        {
            build.Reset();
        }

        if (!wantsGit)
        {
            git.Reset();
        }

        if (wantsBuild)
        {
            var toolRunning = ScenarioProcesses.BuildTools.Any(names!.Contains);
            var change = build.Update(toolRunning, load.CpuPercent);
            if (change == BuildChange.Finished && switches.Allows(PetState.BuildDone))
            {
                moment = (PetState.BuildDone, BuildDoneDuration);
            }

            if (build.IsBuilding && switches.Allows(PetState.Building))
            {
                active.Add(PetState.Building);
            }
        }

        if (wantsGit && git.Update(ScenarioProcesses.GitRemoteHelpers.Any(names!.Contains)))
        {
            moment ??= (PetState.GitPush, GitPushDuration);
        }

        if (foreground is not null
            && switches.Allows(PetState.Reading)
            && ScenarioProcesses.Browsers.Contains(foreground, StringComparer.OrdinalIgnoreCase)
            && devActivity.SinceDeveloping <= options.ReadingWindow)
        {
            active.Add(PetState.Reading);
        }

        if (isDeveloping && switches.Allows(PetState.NightCoding) && lateNight.IsLateNightSession(devActivity.DevelopingFor))
        {
            active.Add(PetState.NightCoding);
        }

        var ambient = ScenarioCatalog.AmbientRanking.Pick(active);
        var isNightCoding = active.Contains(PetState.NightCoding);
        var remark = ambient == PetState.NightCoding && !_wasNightCoding ? ambient : null;
        _wasNightCoding = isNightCoding;

        return new DevScenarioResult(isDeveloping, ambient, moment, remark);
    }
}
