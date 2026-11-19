using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using DeskBuddy.App.Diagnostics;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.ViewModels;
using DeskBuddy.App.Windows;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.Visibility;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Infrastructure.Logging;
using DeskBuddy.Infrastructure.Sensors;
using DeskBuddy.Infrastructure.Startup;
using DeskBuddy.Infrastructure.Pets;
using DeskBuddy.Infrastructure.Storage;
using GitHubReleaseUpdater.AutoUpdate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    /// The named event through which the installer, by starting the application with <see cref="QuitArgument"/>,
    /// asks the running instance to close. Its name is also used by <c>installer/DeskBuddy.iss</c>.
    /// </summary>
    private const string QuitSignalName = @"Local\DeskBuddy.Quit";

    /// <summary>
    /// The named event through which a second start of the application asks the running instance to show itself.
    /// </summary>
    private const string ActivateSignalName = @"Local\DeskBuddy.Activate";

    /// <summary>
    /// The command-line switch that asks the running instance to close and waits until it has.
    /// </summary>
    private const string QuitArgument = "--quit";

    /// <summary>
    /// The name of the folder that holds one subfolder per pet, both next to the program and in the data directory.
    /// </summary>
    private const string PetsFolderName = "pets";

    /// <summary>
    /// How long a start with <see cref="QuitArgument"/> waits for the running instance to close.
    /// </summary>
    private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a full-screen application must be gone before the pet comes back.
    /// </summary>
    private static readonly TimeSpan PetRestoreDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The directory the log files are written to; <see langword="null"/> until startup.
    /// </summary>
    private string? _logDirectory;

    /// <summary>
    /// Writes the rolling log files; <see langword="null"/> until startup.
    /// </summary>
    private Serilog.Core.Logger? _fileLog;

    /// <summary>
    /// Handles errors nobody caught; <see langword="null"/> until startup.
    /// </summary>
    private CrashGuard? _crashGuard;

    /// <summary>
    /// Guard holding the single-instance mutex; <see langword="null"/> until startup.
    /// </summary>
    private SingleInstanceGuard? _instanceGuard;

    /// <summary>
    /// Closes the application when another process asks it to; <see langword="null"/> in a later instance.
    /// </summary>
    private InstanceSignal? _quitSignal;

    /// <summary>
    /// Shows the pet and the panel when the application is started again; <see langword="null"/> in a later instance.
    /// </summary>
    private InstanceSignal? _activateSignal;

    /// <summary>
    /// The folder for the user's own pets, under the data directory; <see langword="null"/> until start-up has worked it
    /// out.
    /// </summary>
    private string? _userPetsDirectory;

    /// <summary>
    /// Puts the pet away while the session is locked; <see langword="null"/> until the pet is shown.
    /// </summary>
    private Microsoft.Win32.SessionSwitchEventHandler? _sessionSwitched;

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
    /// The view model of the panel, created when the application is idle after start-up or when the panel is first opened.
    /// </summary>
    private PanelViewModel? _panelViewModel;

    /// <summary>
    /// The language of the operating system, read when the process started, before the interface language was set. It is
    /// kept because <see cref="CultureInfo.CurrentUICulture"/> holds the interface language from then on, which would make
    /// "follow the system" follow the language that was applied last.
    /// </summary>
    private readonly CultureInfo _systemCulture = CultureInfo.CurrentUICulture;

    /// <summary>
    /// The language setting the interface is written in now.
    /// </summary>
    private AppLanguage _appliedLanguage;

    /// <summary>
    /// The tray icon; <see langword="null"/> until startup completes.
    /// </summary>
    private TrayIconService? _tray;

    /// <summary>
    /// Looks for updates; <see langword="null"/> until start-up has created it, or if updates are not available in this
    /// build. The about tab of the panel offers the check.
    /// </summary>
    private UpdateController? _updates;

    /// <summary>
    /// Reports what goes wrong when the about tab checks for updates.
    /// </summary>
    private ILogger? _updateLogger;

    /// <summary>
    /// Ends the update controller's background loop when the application closes.
    /// </summary>
    private readonly CancellationTokenSource _updatesStopping = new();

    /// <inheritdoc />
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceGuard = new SingleInstanceGuard(SingleInstanceMutexName);
        if (e.Args.Contains(QuitArgument, StringComparer.OrdinalIgnoreCase))
        {
            // Started by the installer to close the running instance cleanly (so it can tidy up, unlike a forced
            // end); the exit code tells whether it has closed in time.
            var closed = _instanceGuard.IsFirstInstance
                || (InstanceSignal.TrySignal(QuitSignalName) && _instanceGuard.WaitForFirstInstanceToExit(QuitTimeout));
            Shutdown(closed ? 0 : 1);
            return;
        }

        if (!_instanceGuard.IsFirstInstance)
        {
            // Starting the application again, from the Start menu or a shortcut, is how a user looks for a pet they
            // hid; the running instance shows itself instead of this start doing nothing visible.
            InstanceSignal.TrySignal(ActivateSignalName);
            Shutdown();
            return;
        }

        _quitSignal = new InstanceSignal(QuitSignalName, () => Dispatcher.BeginInvoke(ExitApplication));
        _activateSignal = new InstanceSignal(ActivateSignalName, () => Dispatcher.BeginInvoke(OnActivationRequested));

        var builder = Host.CreateApplicationBuilder(e.Args);

        // User data lives under LocalAppData so it is not synced by roaming profiles; DataDirectory overrides it.
        var dataDirectory = builder.Configuration["DataDirectory"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskBuddy");

        _logDirectory = Path.Combine(dataDirectory, "logs");
        var logLevel = builder.Configuration.GetValue("FileLog:MinimumLevel", LogLevel.Information);
        _fileLog = LoggerSetup.Create(_logDirectory, logLevel);
        Serilog.SerilogLoggingBuilderExtensions.AddSerilog(builder.Logging, _fileLog, dispose: true);

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
        // The user's own pets live with their data, where installing or upgrading the application never touches them,
        // and win over a shipped pet of the same name.
        _userPetsDirectory = Path.Combine(dataDirectory, PetsFolderName);
        builder.Services.AddSingleton<IPetCatalog>(_ => new FolderPetCatalog(
            _userPetsDirectory,
            Path.Combine(AppContext.BaseDirectory, PetsFolderName)));
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
        ApplyLanguage(_host.Services.GetRequiredService<SettingsService>().Current.Language);
        _host.Services.GetRequiredService<SettingsService>().Changed += OnSettingsChangedForLanguage;
        LogEnvironment(logger);

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
            var (pet, petFrames, loadedName) = await LoadStartupPetAsync(petName, logger);
            petName = loadedName;
            logger.LogInformation(
                "Showing the '{Pet}' pet ({Width}x{Height}, {States} states).",
                petName,
                pet.FrameWidth,
                pet.FrameHeight,
                pet.Animations.Count);

            var brain = _host.Services.GetRequiredService<PetBrain>();
            _sessionSwitched = (_, args) =>
            {
                if (SessionLock.IsLocked(args.Reason) is { } locked)
                {
                    // Raised on a thread of its own.
                    Dispatcher.BeginInvoke(() => brain.SetSessionLocked(locked));
                }
            };
            Microsoft.Win32.SystemEvents.SessionSwitch += _sessionSwitched;
            _petWindow = new PetWindow(
                pet,
                petFrames,
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
                _host.Services.GetRequiredService<IPetCatalog>(),
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
                _previewFrames.Get,
                _previewFrames.PreloadAsync,
                _host.Services.GetRequiredService<ILogger<InstalledPets>>());
            var settingsService = _host.Services.GetRequiredService<SettingsService>();
            var updateLogger = _host.Services.GetRequiredService<ILogger<TrayUpdatePresenter>>();
            var updates = AppUpdates.Create(
                AboutInfo.CurrentVersion(),
                dataDirectory,
                Path.Combine(Path.GetTempPath(), "DeskBuddy", "update"),
                () => settingsService.Current.AutoCheckUpdates,
                new TrayUpdatePresenter(() => _tray, ExitApplication, updateLogger),
                updateLogger);
            _updates = updates;
            _updateLogger = updateLogger;
            _tray = new TrayIconService(
                brain,
                OpenPanel,
                ExitApplication,
                settingsService,
                _installedPets,
                updates,
                _host.Services.GetRequiredService<ILogger<TrayIconService>>());
            if (updates is not null)
            {
                _ = RunUpdatesAsync(updates, updateLogger);
            }

            _hotkeys = new Win32HotkeyService();
            _bossKey = new BossKeyController(settingsService, _hotkeys, brain);
            if (_bossKey.RegistrationError is { } hotkeyError)
            {
                logger.LogWarning(
                    "The boss key {Gesture} could not be registered: {Error}",
                    _host.Services.GetRequiredService<SettingsService>().Current.BossKey,
                    hotkeyError);
            }

            // The panel's list of pets takes a moment to load; do it while nothing else needs the thread.
            _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => PreparePanelViewModel());
        }
        catch (Exception ex) when (PetLoadFailure.Is(ex))
        {
            logger.LogError(ex, "Failed to load the '{Pet}' pet.", petName);
            MessageBox.Show(Strings.PetStartupFailed, Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
        catch (Exception ex)
        {
            // Without a pet window and a tray icon the application cannot be used or even closed, yet it would keep
            // the single-instance lock and make every later start exit silently, so it must not keep running.
            logger.LogCritical(ex, "Start-up failed.");
            MessageBox.Show(Strings.CrashMessage, Strings.CrashTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Records what the application runs on, so a log sent in with a problem tells the version, the system and the
    /// monitor layout without asking.
    /// </summary>
    /// <param name="logger">
    /// Where to write it.
    /// </param>
    private static void LogEnvironment(ILogger logger)
    {
        logger.LogInformation(
            "DeskBuddy {Version} on {OS} ({Architecture}), .NET {Runtime}.",
            AboutInfo.CurrentVersion(),
            Environment.OSVersion.VersionString,
            RuntimeInformation.OSArchitecture,
            Environment.Version);
        foreach (var monitor in MonitorHelper.GetAllMonitors())
        {
            logger.LogInformation(
                "Monitor {Bounds}, work area {WorkArea}, scale {Scale:P0}.",
                monitor.Bounds,
                monitor.WorkArea,
                monitor.Scale);
        }
    }

    /// <summary>
    /// Loads the pet to show at start-up and decodes its pictures off the UI thread. A pet that is missing or broken
    /// is replaced by the default pet, so a bad pet folder does not stop the application from starting.
    /// </summary>
    /// <param name="petName">
    /// The name of the pet in the settings.
    /// </param>
    /// <param name="logger">
    /// Reports a pet that had to be replaced.
    /// </param>
    /// <returns>
    /// The pet, its decoded pictures and the name of the pet actually loaded.
    /// </returns>
    /// <exception cref="Exception">
    /// An exception for which <see cref="PetLoadFailure.Is"/> is <see langword="true"/> when the default pet cannot be
    /// loaded either.
    /// </exception>
    private async Task<(Pet Pet, PetFrameCache Frames, string Name)> LoadStartupPetAsync(string petName, ILogger logger)
    {
        var loader = _host!.Services.GetRequiredService<IPetLoader>();
        var catalog = _host.Services.GetRequiredService<IPetCatalog>();
        if (!string.Equals(petName, PetSettings.DefaultPetName, StringComparison.OrdinalIgnoreCase))
        {
            var petDirectory = catalog.GetPetDirectory(petName);
            if (Directory.Exists(petDirectory))
            {
                try
                {
                    var pet = await loader.LoadAsync(petDirectory);
                    return (pet, await PetFrameCache.CreateAsync(pet), petName);
                }
                catch (Exception ex) when (PetLoadFailure.Is(ex))
                {
                    logger.LogWarning(ex, "The '{Pet}' pet could not be loaded; using '{Default}'.", petName, PetSettings.DefaultPetName);
                }
            }
            else
            {
                logger.LogWarning("The '{Pet}' pet was not found; using '{Default}'.", petName, PetSettings.DefaultPetName);
            }
        }

        var defaultPet = await loader.LoadAsync(catalog.GetPetDirectory(PetSettings.DefaultPetName));
        return (defaultPet, await PetFrameCache.CreateAsync(defaultPet), PetSettings.DefaultPetName);
    }

    /// <inheritdoc />
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Signing out or shutting down must not be held up by the panel, which otherwise hides instead of closing.
        _panel?.PrepareToExit();
        base.OnSessionEnding(e);
    }

    /// <summary>
    /// Runs the update controller's background loop until the application closes, and reports it if it fails.
    /// </summary>
    /// <param name="updates">
    /// The update controller.
    /// </param>
    /// <param name="logger">
    /// Reports why the loop ended.
    /// </param>
    /// <returns>
    /// A task that completes when the loop has ended.
    /// </returns>
    private async Task RunUpdatesAsync(UpdateController updates, ILogger logger)
    {
        try
        {
            await updates.RunAsync(_updatesStopping.Token);
        }
        catch (OperationCanceledException)
        {
            // The application is closing.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The automatic check for updates stopped.");
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Services.GetRequiredService<ILogger<App>>().LogInformation("Exiting with code {ExitCode}.", e.ApplicationExitCode);
        _petSwitcher?.Dispose();
        _themeService?.Dispose();
        _bossKey?.Dispose();
        _hotkeys?.Dispose();
        _updatesStopping.Cancel();
        _updatesStopping.Dispose();
        _tray?.Dispose();
        _previewFrames.Dispose();

        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        if (_sessionSwitched is not null)
        {
            Microsoft.Win32.SystemEvents.SessionSwitch -= _sessionSwitched;
        }

        _activateSignal?.Dispose();
        _quitSignal?.Dispose();
        _instanceGuard?.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Sets the language the interface is written in. Must run on the UI thread, as the current culture belongs to a thread.
    /// </summary>
    /// <param name="language">
    /// The language setting.
    /// </param>
    private void ApplyLanguage(AppLanguage language)
    {
        var culture = LanguageResolver.Resolve(language, _systemCulture);
        Strings.Culture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        _appliedLanguage = language;
        _host?.Services.GetRequiredService<ILogger<App>>().LogInformation("Interface language: {Language}.", culture.Name);
    }

    /// <summary>
    /// Notices a change of the language setting, which may be reported on any thread, and has it applied on the UI thread.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="settings">
    /// The new settings.
    /// </param>
    private void OnSettingsChangedForLanguage(object? sender, PetSettings settings)
    {
        var language = settings.Language;

        _ = Dispatcher.BeginInvoke(() => SwitchLanguage(language));
    }

    /// <summary>
    /// Switches the interface to a language without a restart: the culture is changed, then everything that shows text
    /// writes it again. Windows are not built anew; the text of their XAML follows through <see cref="Loc"/>, and the view
    /// models listen to <see cref="LocalizedStrings.LanguageChanged"/>.
    /// </summary>
    /// <param name="language">
    /// The language setting to switch to.
    /// </param>
    private void SwitchLanguage(AppLanguage language)
    {
        if (language == _appliedLanguage)
        {
            return;
        }

        ApplyLanguage(language);

        LocalizedStrings.Instance.NotifyLanguageChanged();

        if (_installedPets is not null && _host is not null)
        {
            // The names of the pets are written in the language they were loaded in; the tray menu follows once they are.
            _ = RefreshInstalledPetsAsync(_installedPets, _host.Services.GetRequiredService<ILogger<App>>());
        }

        _tray?.RefreshLanguage();
        _petWindow?.RefreshLanguage();
    }

    /// <summary>
    /// Looks at the installed pets again, and reports a failure instead of losing it with the task.
    /// </summary>
    /// <param name="pets">
    /// The installed pets.
    /// </param>
    /// <param name="logger">
    /// Reports a failure.
    /// </param>
    /// <returns>
    /// A task that completes when the pets have been looked at.
    /// </returns>
    private static async Task RefreshInstalledPetsAsync(InstalledPets pets, ILogger logger)
    {
        try
        {
            await pets.RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing waits for this, so any failure (an unreadable pets folder, say) would otherwise go unseen.
            logger.LogWarning(ex, "The pets could not be looked at again after the language changed.");
        }
    }

    /// <summary>
    /// Shows the panel window, creating it the first time.
    /// </summary>
    /// <param name="tab">
    /// The tab to show; <see langword="null"/> keeps the tab that was showing.
    /// </param>
    private void OpenPanel(PanelTab? tab)
    {
        if (_panel is null && PreparePanelViewModel() is { } viewModel)
        {
            _panel = new PanelWindow(viewModel, _updates, _updateLogger);
        }

        _panel?.ShowPanel(tab);
    }

    /// <summary>
    /// Creates the view model of the panel the first time, and starts looking at the pets in it, so that they are ready
    /// when the panel is opened. Called once the application is idle after start-up, and again when the panel opens.
    /// </summary>
    /// <returns>
    /// The view model, or <see langword="null"/> if the application is not up yet.
    /// </returns>
    private PanelViewModel? PreparePanelViewModel()
    {
        if (_panelViewModel is null && _host is not null && _petWindow is not null && _bossKey is not null && _petSwitcher is not null)
        {
            var services = _host.Services;
            _panelViewModel = new PanelViewModel(
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
                    _petSwitcher,
                    _previewFrames.Get,
                    _previewFrames.PreloadAsync,
                    OpenUserPetsDirectory));
            _ = _panelViewModel.Pets.RefreshQuietlyAsync(services.GetRequiredService<ILogger<App>>());
        }

        return _panelViewModel;
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
            () => _fileLog?.Dispose());

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
    /// Opens the folder for the user's own pets in the file manager, creating it first so there is somewhere to put
    /// them.
    /// </summary>
    private void OpenUserPetsDirectory()
    {
        try
        {
            Directory.CreateDirectory(_userPetsDirectory!);
            Process.Start(new ProcessStartInfo(_userPetsDirectory!) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _host?.Services.GetRequiredService<ILogger<App>>().LogWarning(ex, "The pets folder could not be opened.");
        }
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
    /// Shows the pet, if the user had hidden it, and opens the panel, because the application was started again.
    /// Nothing happens while the application is still starting.
    /// </summary>
    private void OnActivationRequested()
    {
        if (_petWindow is null || _host is null)
        {
            return;
        }

        _host.Services.GetRequiredService<PetBrain>().SetManuallyHidden(false);
        OpenPanel(null);
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
