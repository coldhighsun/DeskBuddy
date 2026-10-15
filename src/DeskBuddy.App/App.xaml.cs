using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using DeskBuddy.App.Diagnostics;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.Resources;
using DeskBuddy.App.ViewModels;
using DeskBuddy.App.Windows;
using DeskBuddy.Application;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.Visibility;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Infrastructure.Logging;
using DeskBuddy.Infrastructure.Sensors;
using DeskBuddy.Infrastructure.Startup;
using DeskBuddy.Infrastructure.Pets;
using DeskBuddy.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App;

/// <summary>
/// Application entry point: wires up the generic host, the pet window and the tray icon.
/// </summary>
public partial class App
{
    /// <summary>
    /// Name of the per-session mutex that enforces a single running instance.
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\DeskBuddy.SingleInstance";

    /// <summary>
    /// How long a full-screen application must be gone before the pet comes back.
    /// </summary>
    private static readonly TimeSpan PetRestoreDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many days of log files are kept.
    /// </summary>
    private const int LogRetentionDays = 7;

    /// <summary>
    /// How long to wait for pending log entries to reach the disk when the process is about to die.
    /// </summary>
    private static readonly TimeSpan LogFlushTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The directory the log files are written to; <see langword="null"/> until startup.
    /// </summary>
    private string? _logDirectory;

    /// <summary>
    /// Writes the log files; <see langword="null"/> until startup.
    /// </summary>
    private FileLoggerProvider? _fileLog;

    /// <summary>
    /// Handles errors nobody caught; <see langword="null"/> until startup.
    /// </summary>
    private CrashGuard? _crashGuard;

    /// <summary>
    /// Guard holding the single-instance mutex; <see langword="null"/> until startup.
    /// </summary>
    private SingleInstanceGuard? _instanceGuard;

    /// <summary>
    /// The pet window; <see langword="null"/> until startup completes.
    /// </summary>
    private PetWindow? _petWindow;

    /// <summary>
    /// The panel window; <see langword="null"/> until it is first opened.
    /// </summary>
    private PanelWindow? _panel;

    /// <summary>
    /// The generic host providing DI and logging; <see langword="null"/> until startup.
    /// </summary>
    private IHost? _host;

    /// <summary>
    /// How long the answer about the foreground application is reused, so the parts that ask in one tick share it.
    /// </summary>
    private static readonly TimeSpan ForegroundCacheLifetime = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The shortest time between two walks over the running processes.
    /// </summary>
    private static readonly TimeSpan ProcessListInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Switches the pet when the setting changes; <see langword="null"/> until startup completes.
    /// </summary>
    private PetSwitcher? _petSwitcher;

    /// <summary>
    /// Applies the color theme to the application.
    /// </summary>
    private ThemeService? _themeService;

    /// <summary>
    /// The global boss-key shortcut; <see langword="null"/> until startup completes.
    /// </summary>
    private BossKeyController? _bossKey;

    /// <summary>
    /// Registers the boss-key shortcut with the system; <see langword="null"/> until startup completes.
    /// </summary>
    private Win32HotkeyService? _hotkeys;

    /// <summary>
    /// Decodes the pictures the pet previews of the panel and the pet list of the tray menu show, once for both.
    /// </summary>
    private readonly PetPreviewFrames _previewFrames = new();

    /// <summary>
    /// The installed pets as the tray menu and the settings tab show them; <see langword="null"/> until startup completes.
    /// </summary>
    private InstalledPets? _installedPets;

    /// <summary>
    /// The tray icon; <see langword="null"/> until startup completes.
    /// </summary>
    private TrayIconService? _tray;

    /// <inheritdoc />
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceGuard = new SingleInstanceGuard(SingleInstanceMutexName);
        if (!_instanceGuard.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        var builder = Host.CreateApplicationBuilder(e.Args);

        // User data lives under LocalAppData so it is not synced by roaming profiles; DataDirectory overrides it.
        var dataDirectory = builder.Configuration["DataDirectory"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskBuddy");

        _logDirectory = Path.Combine(dataDirectory, "logs");
        var logLevel = builder.Configuration.GetValue("FileLog:MinimumLevel", LogLevel.Information);
        _fileLog = new FileLoggerProvider(_logDirectory, TimeProvider.System, logLevel, LogRetentionDays);
        builder.Logging.AddProvider(_fileLog);

        var behaviorOptions = new PetBehaviorOptions();
        var loadOptions = new SystemLoadOptions();
        var sedentaryOptions = new SedentaryOptions();

        // Timings can be forced through configuration, e.g. the environment variable Behavior__SleepAfter. These
        // overrides are for diagnostics and win over the user's saved settings, so they are re-applied after each change.
        void ApplyConfigurationOverrides()
        {
            builder.Configuration.GetSection("Behavior").Bind(behaviorOptions);
            builder.Configuration.GetSection("SystemLoad").Bind(loadOptions);
            builder.Configuration.GetSection("Sedentary").Bind(sedentaryOptions);
        }

        ApplyConfigurationOverrides();

        builder.Services.AddSingleton(behaviorOptions);
        builder.Services.AddSingleton(loadOptions);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IPetLoader, FolderPetLoader>();
        builder.Services.AddSingleton<IAutoStartService>(_ => new RegistryAutoStartService());
        builder.Services.AddSingleton<IPetCatalog>(_ => new FolderPetCatalog(Path.Combine(AppContext.BaseDirectory, "pets")));
        builder.Services.AddSingleton<IIdleDetector, Win32IdleDetector>();
        builder.Services.AddSingleton<ISystemLoadMonitor, WindowsSystemLoadMonitor>();
        builder.Services.AddSingleton<LoadReactionPolicy>();
        builder.Services.AddSingleton<IFullscreenDetector, WindowsFullscreenDetector>();
        builder.Services.AddSingleton<WindowsForegroundAppDetector>();
        builder.Services.AddSingleton<IForegroundAppDetector>(sp => new CachedForegroundApp(
            sp.GetRequiredService<WindowsForegroundAppDetector>(),
            sp.GetRequiredService<TimeProvider>(),
            ForegroundCacheLifetime));
        builder.Services.AddSingleton<WindowsProcessList>();
        builder.Services.AddSingleton<IProcessList>(sp => new ThrottledProcessList(
            sp.GetRequiredService<WindowsProcessList>(),
            sp.GetRequiredService<TimeProvider>(),
            ProcessListInterval));
        builder.Services.AddSingleton<IPowerStatusProvider, WindowsPowerStatusProvider>();
        builder.Services.AddSingleton<MeetingPolicy>();
        builder.Services.AddSingleton<ChatPolicy>();
        builder.Services.AddSingleton(sp => new WelcomeBackPolicy(sp.GetRequiredService<PetBehaviorOptions>()));
        builder.Services.AddSingleton(sp => new PowerPolicy(sp.GetRequiredService<PetBehaviorOptions>()));
        builder.Services.AddSingleton(sp => new ScheduleClock(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ScenarioSwitches>()));
        builder.Services.AddSingleton<SystemScenarios>();
        builder.Services.AddSingleton<ScenarioSwitches>();
        builder.Services.AddSingleton(sp => new DevActivityPolicy(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new BuildActivityPolicy(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new GitSyncPolicy(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new LateNightPolicy(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<DevScenarios>();
        builder.Services.AddSingleton(sp => new ChatterPolicy(sp.GetRequiredService<TimeProvider>(), Random.Shared));
        builder.Services.AddSingleton<DockedMovement>();
        builder.Services.AddSingleton(sp => new PetVisibility(sp.GetRequiredService<TimeProvider>(), PetRestoreDelay));
        builder.Services.AddSingleton(sedentaryOptions);
        builder.Services.AddSingleton<SedentaryReminderPolicy>();
        builder.Services.AddSingleton(sp => new PetMessageQueue(
            behaviorOptions.MessageDuration,
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new PetBehaviorStateMachine(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>(),
            Random.Shared));
        builder.Services.AddSingleton<PetBrain>();

        builder.Services.AddSingleton<ISettingsStore>(sp => new JsonSettingsStore(
            dataDirectory,
            sp.GetRequiredService<ILogger<JsonSettingsStore>>()));
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton(sp => new SettingsApplier(
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

        _host = builder.Build();
        InstallCrashGuard();
        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        await InitializeUserDataAsync(logger);

        // The language must be set before any window or the tray icon reads a string.
        var culture = LanguageResolver.Resolve(
            _host.Services.GetRequiredService<SettingsService>().Current.Language,
            CultureInfo.CurrentUICulture);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        logger.LogInformation("Interface language: {Language}.", culture.Name);

        // The theme must be applied before any window is created, so none flashes in the wrong colors.
        var themeService = new ThemeService(
            Resources,
            Dispatcher,
            _host.Services.GetRequiredService<SettingsService>(),
            _host.Services.GetRequiredService<ILogger<ThemeService>>());
        _themeService = themeService;

        // Resolving the applier copies the loaded settings into the option objects the pet logic reads from.
        _host.Services.GetRequiredService<SettingsApplier>();
        var petName = _host.Services.GetRequiredService<SettingsService>().Current.PetName;

        try
        {
            var petDirectory = Path.Combine(AppContext.BaseDirectory, "pets", petName);
            if (!Directory.Exists(petDirectory))
            {
                logger.LogWarning("The '{Pet}' pet was not found; using '{Default}'.", petName, PetSettings.DefaultPetName);
                petName = PetSettings.DefaultPetName;
                petDirectory = Path.Combine(AppContext.BaseDirectory, "pets", petName);
            }

            var pet = await _host.Services.GetRequiredService<IPetLoader>().LoadAsync(petDirectory);

            var brain = _host.Services.GetRequiredService<PetBrain>();
            _petWindow = new PetWindow(
                pet,
                new PetFrameCache(pet),
                brain,
                behaviorOptions,
                OpenPanel,
                _host.Services.GetRequiredService<DockedMovement>(),
                _host.Services.GetRequiredService<ScenarioSwitches>(),
                _host.Services.GetRequiredService<SettingsService>().Current.Size);
            _petWindow.SetClimbEdges(_host.Services.GetRequiredService<SettingsService>().Current.ClimbEdges);
            _petWindow.Show();
            var petWindow = _petWindow;
            _host.Services.GetRequiredService<SettingsService>().Changed +=
                (_, settings) => petWindow.Dispatcher.BeginInvoke(() =>
                {
                    petWindow.SetSize(settings.Size);
                    petWindow.SetClimbEdges(settings.ClimbEdges);
                });

            // Decoding all the pictures of a pet takes long enough to freeze the UI, so it is done on a thread of its own.
            var preparedFrames = new ConditionalWeakTable<Pet, PetFrameCache>();
            _petSwitcher = new PetSwitcher(
                _host.Services.GetRequiredService<SettingsService>(),
                _host.Services.GetRequiredService<IPetLoader>(),
                Path.Combine(AppContext.BaseDirectory, "pets"),
                petName,
                newPet =>
                {
                    var frames = preparedFrames.TryGetValue(newPet, out var prepared) ? prepared : new PetFrameCache(newPet);
                    petWindow.SetPet(newPet, frames);
                },
                _host.Services.GetRequiredService<ILogger<PetSwitcher>>(),
                async newPet => preparedFrames.AddOrUpdate(newPet, await PetFrameCache.CreateAsync(newPet)));
            _installedPets = new InstalledPets(
                _host.Services.GetRequiredService<IPetCatalog>(),
                _host.Services.GetRequiredService<IPetLoader>(),
                Path.Combine(AppContext.BaseDirectory, "pets"),
                _previewFrames.Get);
            _tray = new TrayIconService(
                brain,
                OpenPanel,
                ExitApplication,
                themeService,
                _host.Services.GetRequiredService<SettingsService>(),
                _installedPets,
                _host.Services.GetRequiredService<ILogger<TrayIconService>>());

            _hotkeys = new Win32HotkeyService();
            _bossKey = new BossKeyController(_host.Services.GetRequiredService<SettingsService>(), _hotkeys, brain);
            if (_bossKey.RegistrationError is { } hotkeyError)
            {
                logger.LogWarning(
                    "The boss key {Gesture} could not be registered: {Error}",
                    _host.Services.GetRequiredService<SettingsService>().Current.BossKey,
                    hotkeyError);
            }
        }
        catch (InvalidDataException ex)
        {
            logger.LogError(ex, "Failed to load the '{Pet}' pet.", petName);
            MessageBox.Show(ex.Message, "DeskBuddy", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _petSwitcher?.Dispose();
        _themeService?.Dispose();
        _bossKey?.Dispose();
        _hotkeys?.Dispose();
        _tray?.Dispose();

        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        _instanceGuard?.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Shows the panel window, creating it the first time.
    /// </summary>
    /// <param name="tab">
    /// The tab to show; <see langword="null"/> keeps the tab that was showing.
    /// </param>
    private void OpenPanel(PanelTab? tab)
    {
        if (_panel is null && _host is not null && _petWindow is not null && _bossKey is not null && _petSwitcher is not null)
        {
            var services = _host.Services;
            var viewModel = new PanelViewModel(
                new SettingsTabViewModel(
                    services.GetRequiredService<SettingsService>(),
                    _bossKey,
                    services.GetRequiredService<IAutoStartService>(),
                    OpenLogDirectory,
                    _installedPets),
                new PetsTabViewModel(
                    services.GetRequiredService<SettingsService>(),
                    services.GetRequiredService<IPetCatalog>(),
                    services.GetRequiredService<IPetLoader>(),
                    Path.Combine(AppContext.BaseDirectory, "pets"),
                    _petSwitcher,
                    _previewFrames.Get));
            _panel = new PanelWindow(viewModel);
            _themeService?.Register(_panel);
        }

        _panel?.ShowPanel(tab);
    }

    /// <summary>
    /// Hooks the three places where an error can escape, so it is logged instead of vanishing.
    /// </summary>
    private void InstallCrashGuard()
    {
        _crashGuard = new CrashGuard(
            _host!.Services.GetRequiredService<ILogger<App>>(),
            TimeProvider.System,
            () => _tray?.ShowErrorNotice(Strings.CrashTitle, Strings.CrashMessage),
            () => _fileLog?.Flush(LogFlushTimeout));

        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = _crashGuard.HandleUiException(args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => _crashGuard.HandleFatal(args.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _crashGuard.HandleUnobservedTask(args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>
    /// Opens the log directory in the file manager.
    /// </summary>
    private void OpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory!);
            Process.Start(new ProcessStartInfo(_logDirectory!) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _host?.Services.GetRequiredService<ILogger<App>>().LogWarning(ex, "The log directory could not be opened.");
        }
    }

    /// <summary>
    /// Exits the application, letting the panel window really close instead of just hiding.
    /// </summary>
    private void ExitApplication()
    {
        _panel?.PrepareToExit();
        Shutdown();
    }

    /// <summary>
    /// Loads the saved settings. A storage problem does not stop the application; it is logged here and
    /// shown in the panel, where editing is disabled so the unreadable data is never overwritten.
    /// </summary>
    /// <param name="logger">
    /// Logger used to report storage problems.
    /// </param>
    /// <returns>
    /// A task that completes when they have been loaded.
    /// </returns>
    private async Task InitializeUserDataAsync(ILogger logger)
    {
        var services = _host!.Services;

        var settings = services.GetRequiredService<SettingsService>();
        await settings.InitializeAsync();
        if (settings.LoadFailure is { } settingsFailure)
        {
            logger.LogError(settingsFailure, "The saved settings could not be read; defaults are used and changes are disabled.");
        }

        SettingsLog.LogRemovedProcessNames(logger, settings.RemovedAtLoad);
    }
}
