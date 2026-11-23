using System.IO;
using DeskBuddy.App.Diagnostics;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;
using DeskBuddy.Infrastructure.Pets;
using DeskBuddy.Infrastructure.Sensors;
using DeskBuddy.Infrastructure.Startup;
using DeskBuddy.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeskBuddy.App;

/// <summary>
/// Registers the services of the application with the generic host: the sensors, the behavior policies, the settings
/// and the pet catalog.
/// </summary>
internal static class AppServices
{
    /// <summary>
    /// The name of the folder that holds one subfolder per pet, both next to the program and in the data directory.
    /// </summary>
    public const string PetsFolderName = "pets";

    /// <summary>
    /// How long the answer about a full-screen program is reused before it is read again.
    /// </summary>
    private static readonly TimeSpan FullscreenInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long the polled foreground program is remembered before it is asked for again; only used until the hook of the
    /// foreground window has reported, or if it cannot be installed.
    /// </summary>
    private static readonly TimeSpan ForegroundInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How long the list of running processes is reused before it is read again.
    /// </summary>
    private static readonly TimeSpan ProcessListInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a pushed power status is trusted before it is read again. The system does not announce every percent of
    /// charge, and reading is one cheap call, so this is short: a battery that drops below the low-battery limit is
    /// noticed within this time, and the pushes only make the plugging in and out immediate.
    /// </summary>
    private static readonly TimeSpan PowerStatusMaxAge = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long the pet stays away after a full-screen program or a session lock ends.
    /// </summary>
    private static readonly TimeSpan PetRestoreDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Adds every service the application uses.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="configuration">
    /// The configuration, whose <c>Behavior</c>, <c>SystemLoad</c> and <c>Sedentary</c> sections may force timings.
    /// </param>
    /// <param name="dataDirectory">The directory that holds the settings and the user's own pets.</param>
    /// <param name="userPetsDirectory">
    /// The directory for the user's own pets, which win over a shipped pet of the same name.
    /// </param>
    public static void AddDeskBuddyServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string dataDirectory,
        string userPetsDirectory)
    {
        var behaviorOptions = new PetBehaviorOptions();
        var loadOptions = new SystemLoadOptions();
        var sedentaryOptions = new SedentaryOptions();

        // Timings can be forced through configuration, e.g. the environment variable Behavior__SleepAfter. These
        // overrides are for diagnostics and win over the user's saved settings, so they are re-applied after each change.
        void ApplyConfigurationOverrides()
        {
            configuration.GetSection("Behavior").Bind(behaviorOptions);
            configuration.GetSection("SystemLoad").Bind(loadOptions);
            configuration.GetSection("Sedentary").Bind(sedentaryOptions);
        }

        ApplyConfigurationOverrides();

        services.AddSingleton(behaviorOptions);
        services.AddSingleton(loadOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPetLoader, FolderPetLoader>();
        services.AddSingleton<IAutoStartService>(_ => new RegistryAutoStartService());
        services.AddSingleton<IPetCatalog>(sp => new FolderPetCatalog(
            userPetsDirectory,
            Path.Combine(AppContext.BaseDirectory, PetsFolderName),
            sp.GetRequiredService<ILogger<FolderPetCatalog>>()));

        services.AddSensors();
        services.AddPolicies(sedentaryOptions, behaviorOptions);

        services.AddSingleton<ISettingsStore>(sp => new JsonSettingsStore(
            dataDirectory,
            sp.GetRequiredService<ILogger<JsonSettingsStore>>()));
        services.AddSingleton<SettingsService>();
        services.AddSingleton(sp => new SettingsApplier(
            sp.GetRequiredService<SettingsService>(),
            behaviorOptions,
            sedentaryOptions,
            sp.GetRequiredService<PetVisibility>(),
            sp.GetRequiredService<ChatterPolicy>(),
            sp.GetRequiredService<DevActivityPolicy>(),
            sp.GetRequiredService<ScenarioSwitches>(),
            sp.GetRequiredService<MeetingPolicy>(),
            sp.GetRequiredService<ChatPolicy>(),
            sp.GetRequiredService<AutoSwitchPolicy>(),
            ApplyConfigurationOverrides));
    }

    /// <summary>
    /// Adds the Windows sensors. The ones that ask another program (the shell, the foreground process, the list of
    /// processes) are read on a thread of the thread pool and answer from their last reading, so that a program that is slow
    /// to answer cannot freeze the pet; the rest are single cheap system calls and are read where they are asked.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    private static void AddSensors(this IServiceCollection services)
    {
        services.AddSingleton(sp => new SlowSensorLog(sp.GetRequiredService<ILogger<SlowSensorLog>>()));
        services.AddSingleton<IIdleDetector, Win32IdleDetector>();
        services.AddSingleton<ISystemLoadMonitor, WindowsSystemLoadMonitor>();
        services.AddSingleton<WindowsForegroundAppDetector>();
        services.AddSingleton(sp =>
        {
            var inner = new WindowsFullscreenDetector(
                sp.GetRequiredService<SettingsService>(),
                sp.GetRequiredService<IForegroundAppDetector>());
            var detector = new BackgroundFullscreenDetector(new BackgroundSensor<DoNotDisturbLevel>(
                "full-screen check (background)",
                inner.GetLevel,
                DoNotDisturbLevel.None,
                FullscreenInterval,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<SlowSensorLog>().Diagnostics));

            // Another window coming to the front may be one that covers the screen, or the end of one: read at once
            // instead of at the next tick of the brain, so the pet goes away and comes back as soon as the user does.
            sp.GetRequiredService<PushedForegroundApp>().Changed += (_, _) => detector.Refresh();
            return detector;
        });
        services.AddSingleton<IFullscreenDetector>(sp => sp.GetRequiredService<BackgroundFullscreenDetector>());

        // Windows says when another window comes to the front, so nothing needs to be polled for the program in front; the
        // polling detector only answers until the first push, and if the hook cannot be installed.
        services.AddSingleton(sp => new PushedForegroundApp(new BackgroundForegroundApp(
            new BackgroundSensor<string?>(
                "foreground program (background)",
                sp.GetRequiredService<WindowsForegroundAppDetector>().GetForegroundProcessName,
                null,
                ForegroundInterval,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<SlowSensorLog>().Diagnostics))));
        services.AddSingleton(sp => new WinEventForegroundHook(
            sp.GetRequiredService<PushedForegroundApp>().Push,
            sp.GetRequiredService<ILogger<WinEventForegroundHook>>()));
        services.AddSingleton<IForegroundAppDetector>(sp =>
        {
            // Resolving the hook installs it; the container removes it again when the application ends.
            _ = sp.GetRequiredService<WinEventForegroundHook>();
            return sp.GetRequiredService<PushedForegroundApp>();
        });
        services.AddSingleton<WindowsProcessList>();
        services.AddSingleton<IProcessList>(sp => new BackgroundProcessList(
            new BackgroundSensor<IReadOnlySet<string>>(
                "process list (background)",
                sp.GetRequiredService<WindowsProcessList>().GetNames,
                new HashSet<string>(),
                ProcessListInterval,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<SlowSensorLog>().Diagnostics)));

        // Windows says when the power supply changes; a pushed status is read again after a while all the same, as not
        // every percent of charge is announced.
        services.AddSingleton<WindowsPowerStatusProvider>();
        services.AddSingleton(sp => new PushedPowerStatus(
            sp.GetRequiredService<WindowsPowerStatusProvider>(),
            sp.GetRequiredService<TimeProvider>(),
            PowerStatusMaxAge));
        services.AddSingleton(sp => new SystemEventsPowerWatcher(
            sp.GetRequiredService<WindowsPowerStatusProvider>(),
            sp.GetRequiredService<PushedPowerStatus>().Push,
            sp.GetRequiredService<ILogger<SystemEventsPowerWatcher>>()));
        services.AddSingleton<IPowerStatusProvider>(sp =>
        {
            // Resolving the watcher starts it; the container stops it again when the application ends.
            _ = sp.GetRequiredService<SystemEventsPowerWatcher>();
            return sp.GetRequiredService<PushedPowerStatus>();
        });
    }

    /// <summary>
    /// Adds the policies, the scenarios and the state machine that decide what the pet does.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="sedentaryOptions">The options of the sit-too-long reminder.</param>
    /// <param name="behaviorOptions">The timings of the pet's behavior.</param>
    private static void AddPolicies(
        this IServiceCollection services,
        SedentaryOptions sedentaryOptions,
        PetBehaviorOptions behaviorOptions)
    {
        services.AddSingleton<LoadReactionPolicy>();
        services.AddSingleton<MeetingPolicy>();
        services.AddSingleton<ChatPolicy>();
        services.AddSingleton(sp => new WelcomeBackPolicy(sp.GetRequiredService<PetBehaviorOptions>()));
        services.AddSingleton(sp => new PowerPolicy(sp.GetRequiredService<PetBehaviorOptions>()));
        services.AddSingleton(sp => new ScheduleClock(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ScenarioSwitches>()));
        services.AddSingleton<SystemScenarios>();
        services.AddSingleton<ScenarioSwitches>();
        services.AddSingleton(sp => new DevActivityPolicy(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new BuildActivityPolicy(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new GitSyncPolicy(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new LateNightPolicy(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<DevScenarios>();
        services.AddSingleton(sp => new ChatterPolicy(sp.GetRequiredService<TimeProvider>(), Random.Shared));
        services.AddSingleton(sp => new AutoSwitchPolicy(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<DockedMovement>();
        services.AddSingleton(sp => new PetVisibility(sp.GetRequiredService<TimeProvider>(), PetRestoreDelay));
        services.AddSingleton(sedentaryOptions);
        services.AddSingleton<SedentaryReminderPolicy>();
        services.AddSingleton(sp => new PetMessageQueue(
            behaviorOptions.MessageDuration,
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new PetBehaviorStateMachine(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>(),
            Random.Shared));
        services.AddSingleton<PetBrain>();
    }
}
