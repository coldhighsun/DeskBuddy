using System.IO;
using System.Windows;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.ViewModels;
using DeskBuddy.App.Windows;
using DeskBuddy.Application;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Movement;
using DeskBuddy.Domain.Pomodoro;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.Visibility;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Infrastructure.Sensors;
using DeskBuddy.Infrastructure.Skins;
using DeskBuddy.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.App;

/// <summary>
/// Application entry point: wires up the generic host, the pet window and the tray icon.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Name of the per-session mutex that enforces a single running instance.
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\DeskBuddy.SingleInstance";

    /// <summary>
    /// How long after the last keystroke the note is saved.
    /// </summary>
    private static readonly TimeSpan NoteSaveDelay = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// How long a full-screen application must be gone before the pet comes back.
    /// </summary>
    private static readonly TimeSpan PetRestoreDelay = TimeSpan.FromSeconds(2);

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
    /// The global boss-key shortcut; <see langword="null"/> until startup completes.
    /// </summary>
    private BossKeyController? _bossKey;

    /// <summary>
    /// Registers the boss-key shortcut with the system; <see langword="null"/> until startup completes.
    /// </summary>
    private Win32HotkeyService? _hotkeys;

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

        var behaviorOptions = new PetBehaviorOptions();
        var loadOptions = new SystemLoadOptions();
        var pomodoroOptions = new PomodoroOptions();
        var sedentaryOptions = new SedentaryOptions();
        var roamOptions = new RoamOptions();

        // Timings can be forced through configuration, e.g. the environment variable Behavior__SleepAfter. These
        // overrides are for diagnostics and win over the user's saved settings, so they are re-applied after each change.
        void ApplyConfigurationOverrides()
        {
            builder.Configuration.GetSection("Behavior").Bind(behaviorOptions);
            builder.Configuration.GetSection("SystemLoad").Bind(loadOptions);
            builder.Configuration.GetSection("Pomodoro").Bind(pomodoroOptions);
            builder.Configuration.GetSection("Sedentary").Bind(sedentaryOptions);
            builder.Configuration.GetSection("Roam").Bind(roamOptions);
        }

        ApplyConfigurationOverrides();

        builder.Services.AddSingleton(behaviorOptions);
        builder.Services.AddSingleton(loadOptions);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISkinLoader, FolderSkinLoader>();
        builder.Services.AddSingleton<IIdleDetector, Win32IdleDetector>();
        builder.Services.AddSingleton<ISystemLoadMonitor, WindowsSystemLoadMonitor>();
        builder.Services.AddSingleton<LoadReactionPolicy>();
        builder.Services.AddSingleton<IFullscreenDetector, WindowsFullscreenDetector>();
        builder.Services.AddSingleton(roamOptions);
        builder.Services.AddSingleton(sp => new ChatterPolicy(sp.GetRequiredService<TimeProvider>(), Random.Shared));
        builder.Services.AddSingleton<DockedMovement>();
        builder.Services.AddSingleton(sp => new RoamMovement(
            sp.GetRequiredService<RoamOptions>(),
            sp.GetRequiredService<TimeProvider>(),
            Random.Shared));
        builder.Services.AddSingleton<MovementSelector>();
        builder.Services.AddSingleton(sp => new PetVisibility(sp.GetRequiredService<TimeProvider>(), PetRestoreDelay));
        builder.Services.AddSingleton(pomodoroOptions);
        builder.Services.AddSingleton<PomodoroSession>();
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
            pomodoroOptions,
            sedentaryOptions,
            sp.GetRequiredService<PetVisibility>(),
            sp.GetRequiredService<ChatterPolicy>(),
            ApplyConfigurationOverrides));
        builder.Services.AddSingleton<ITodoRepository>(sp => new JsonTodoRepository(
            dataDirectory,
            sp.GetRequiredService<ILogger<JsonTodoRepository>>()));
        builder.Services.AddSingleton<INoteStore>(_ => new FileNoteStore(dataDirectory));
        builder.Services.AddSingleton<TodoService>();
        builder.Services.AddSingleton(sp => new NoteService(
            sp.GetRequiredService<INoteStore>(),
            sp.GetRequiredService<TimeProvider>(),
            NoteSaveDelay));

        _host = builder.Build();
        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        await InitializeUserDataAsync(logger);

        // Resolving the applier copies the loaded settings into the option objects the pet logic reads from.
        _host.Services.GetRequiredService<SettingsApplier>();
        var skinName = _host.Services.GetRequiredService<SettingsService>().Current.SkinName;

        try
        {
            var skinDirectory = Path.Combine(AppContext.BaseDirectory, "skins", skinName);
            if (!Directory.Exists(skinDirectory))
            {
                logger.LogWarning("The '{Skin}' skin was not found; using '{Default}'.", skinName, PetSettings.DefaultSkinName);
                skinName = PetSettings.DefaultSkinName;
                skinDirectory = Path.Combine(AppContext.BaseDirectory, "skins", skinName);
            }

            var skin = await _host.Services.GetRequiredService<ISkinLoader>().LoadAsync(skinDirectory);

            var brain = _host.Services.GetRequiredService<PetBrain>();
            _petWindow = new PetWindow(
                skin,
                new SkinFrameCache(skin),
                brain,
                behaviorOptions,
                OpenPanel,
                _host.Services.GetRequiredService<MovementSelector>());
            _petWindow.Show();
            _tray = new TrayIconService(brain, _petWindow.RefreshState, OpenPanel, ExitApplication);

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
            logger.LogError(ex, "Failed to load the '{Skin}' skin.", skinName);
            MessageBox.Show(ex.Message, "DeskBuddy", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _bossKey?.Dispose();
        _hotkeys?.Dispose();
        _tray?.Dispose();

        if (_host is not null)
        {
            FlushNote(_host.Services.GetRequiredService<ILogger<App>>());
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        _instanceGuard?.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Shows the panel window, creating it the first time.
    /// </summary>
    private void OpenPanel()
    {
        if (_panel is null && _host is not null && _petWindow is not null)
        {
            var services = _host.Services;
            var viewModel = new PanelViewModel(
                new PomodoroTabViewModel(services.GetRequiredService<PetBrain>(), _petWindow.RefreshState),
                new TodoTabViewModel(services.GetRequiredService<TodoService>()),
                new NoteTabViewModel(services.GetRequiredService<NoteService>()));
            _panel = new PanelWindow(viewModel, services.GetRequiredService<NoteService>());
        }

        _panel?.ShowPanel();
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
    /// Loads the saved settings, to-do list and note. A storage problem does not stop the application; it is logged here and
    /// shown in the panel, where editing is disabled so the unreadable data is never overwritten.
    /// </summary>
    /// <param name="logger">
    /// Logger used to report storage problems.
    /// </param>
    /// <returns>
    /// A task that completes when all have been loaded.
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

        var todos = services.GetRequiredService<TodoService>();
        await todos.InitializeAsync();
        if (todos.LoadFailure is { } todoFailure)
        {
            logger.LogError(todoFailure, "The saved to-do list could not be read; changes are disabled.");
        }

        var note = services.GetRequiredService<NoteService>();
        await note.InitializeAsync();
        if (note.LoadFailure is { } noteFailure)
        {
            logger.LogError(noteFailure, "The saved note could not be read; edits are disabled.");
        }
    }

    /// <summary>
    /// Saves any note text typed in the last moments before exit.
    /// </summary>
    /// <param name="logger">
    /// Logger used to report a failed save.
    /// </param>
    private void FlushNote(ILogger logger)
    {
        try
        {
            _host!.Services.GetRequiredService<NoteService>().FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "The note could not be saved on exit.");
        }
    }
}
