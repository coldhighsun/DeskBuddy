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
    /// How long a start with <see cref="QuitArgument"/> waits for the running instance to close.
    /// </summary>
    private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(10);

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
    /// Logs when the UI thread stops answering; <see langword="null"/> until the pet window is up.
    /// </summary>
    private UiStallMonitor? _stallMonitor;

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

        try
        {
            await StartApplicationAsync(e);
        }
        catch (Exception ex)
        {
            // Whatever the more specific handlers further in did not catch (the host, the settings, the theme): without a
            // window and a tray icon the application could be neither used nor closed, yet it would keep the single-instance
            // lock and make every later start exit silently, so it must not keep running.
            if (_host?.Services.GetService<ILogger<App>>() is { } log)
            {
                log.LogCritical(ex, "Start-up failed.");
            }
            else
            {
                _fileLog?.Fatal(ex, "Start-up failed.");
            }

            MessageBox.Show(Strings.CrashMessage, Strings.CrashTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Sets up the host, the settings, the pet window and the tray icon. Called once the start has been found to be the first
    /// instance's, and wrapped by <see cref="OnStartup"/> so that a failure anywhere in it ends the application.
    /// </summary>
    /// <param name="e">The start-up arguments.</param>
    /// <returns>A task that completes when the application is up.</returns>
    private async Task StartApplicationAsync(StartupEventArgs e)
    {
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

        // Where the user's own pets live: with their data, where installing or upgrading the application never touches
        // them, and they win over a shipped pet of the same name.
        _userPetsDirectory = Path.Combine(dataDirectory, AppServices.PetsFolderName);
        builder.Services.AddDeskBuddyServices(builder.Configuration, dataDirectory, _userPetsDirectory);

        _host = builder.Build();
        InstallCrashGuard();
        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        StartupDiagnostics.LowerPriority(logger);
        await InitializeUserDataAsync(logger);

        // The language must be set before any window or the tray icon reads a string.
        ApplyLanguage(_host.Services.GetRequiredService<SettingsService>().Current.Language);
        _host.Services.GetRequiredService<SettingsService>().Changed += OnSettingsChangedForLanguage;
        StartupDiagnostics.LogEnvironment(logger);

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
            var (pet, petFrames, loadedName) = await StartupPet.LoadAsync(
                _host.Services.GetRequiredService<IPetLoader>(),
                _host.Services.GetRequiredService<IPetCatalog>(),
                petName,
                logger);
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
                _host.Services.GetRequiredService<PetBehaviorOptions>(),
                OpenPanel,
                _host.Services.GetRequiredService<DockedMovement>(),
                _host.Services.GetRequiredService<ScenarioSwitches>(),
                _host.Services.GetRequiredService<SettingsService>().Current.Size);
            _petWindow.Show();
            var petWindow = _petWindow;
            petWindow.TickTimed = new SlowTickLog(logger, TimeProvider.System).Report;
            _stallMonitor = new UiStallMonitor(
                Dispatcher,
                logger,
                TimeProvider.System,
                () => $"pet window: {petWindow.Describe()}; panel visible: {_panel?.IsVisible == true}");
            _stallMonitor.Start();
            _host.Services.GetRequiredService<SettingsService>().Changed +=
                (_, settings) => petWindow.Dispatcher.BeginInvoke(() => petWindow.SetSize(settings.Size));

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
                async newPet => preparedFrames.AddOrUpdate(newPet, await PetFrameCache.CreateAsync(newPet)),
                () => petWindow.PlayLeaveAsync(_host.Services.GetRequiredService<SettingsService>().Current.SwitchEffect));
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
    /// <param name="updates">The update controller.</param>
    /// <param name="logger">Reports why the loop ended.</param>
    /// <returns>A task that completes when the loop has ended.</returns>
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
        _stallMonitor?.Dispose();
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
    /// <param name="logger">Logger used to report storage problems.</param>
    /// <returns>A task that completes when they have been loaded.</returns>
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
