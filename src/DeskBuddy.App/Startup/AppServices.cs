using System.IO;
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
    /// How long the foreground program is remembered before it is asked for again.
    /// </summary>
    private static readonly TimeSpan ForegroundCacheLifetime = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How long the list of running processes is reused before it is read again.
    /// </summary>
    private static readonly TimeSpan ProcessListInterval = TimeSpan.FromSeconds(3);

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
        services.AddSingleton<IPetCatalog>(_ => new FolderPetCatalog(
            userPetsDirectory,
            Path.Combine(AppContext.BaseDirectory, PetsFolderName)));

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
            ApplyConfigurationOverrides));
    }

    /// <summary>
    /// Adds the Windows sensors, some of them cached or throttled.
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    private static void AddSensors(this IServiceCollection services)
    {
        services.AddSingleton<IIdleDetector, Win32IdleDetector>();
        services.AddSingleton<ISystemLoadMonitor, WindowsSystemLoadMonitor>();
        services.AddSingleton<IFullscreenDetector, WindowsFullscreenDetector>();
        services.AddSingleton<WindowsForegroundAppDetector>();
        services.AddSingleton<IForegroundAppDetector>(sp => new CachedForegroundApp(
            sp.GetRequiredService<WindowsForegroundAppDetector>(),
            sp.GetRequiredService<TimeProvider>(),
            ForegroundCacheLifetime));
        services.AddSingleton<WindowsProcessList>();
        services.AddSingleton<IProcessList>(sp => new ThrottledProcessList(
            sp.GetRequiredService<WindowsProcessList>(),
            sp.GetRequiredService<TimeProvider>(),
            ProcessListInterval));
        services.AddSingleton<IPowerStatusProvider, WindowsPowerStatusProvider>();
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
