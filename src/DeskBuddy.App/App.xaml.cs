using System.IO;
using System.Windows;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.Windows;
using DeskBuddy.Application;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Infrastructure.Sensors;
using DeskBuddy.Infrastructure.Skins;
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
    /// Directory name of the skin loaded at startup, under the <c>skins</c> folder.
    /// </summary>
    private const string DefaultSkinName = "default";

    /// <summary>
    /// Guard holding the single-instance mutex; <see langword="null"/> until startup.
    /// </summary>
    private SingleInstanceGuard? _instanceGuard;

    /// <summary>
    /// The generic host providing DI and logging; <see langword="null"/> until startup.
    /// </summary>
    private IHost? _host;

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

        // Timings can be overridden through configuration, e.g. the environment variable Behavior__SleepAfter.
        var behaviorOptions = new PetBehaviorOptions();
        builder.Configuration.GetSection("Behavior").Bind(behaviorOptions);

        builder.Services.AddSingleton(behaviorOptions);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISkinLoader, FolderSkinLoader>();
        builder.Services.AddSingleton<IIdleDetector, Win32IdleDetector>();
        builder.Services.AddSingleton(sp => new PetBehaviorStateMachine(
            sp.GetRequiredService<PetBehaviorOptions>(),
            sp.GetRequiredService<TimeProvider>(),
            Random.Shared));
        builder.Services.AddSingleton<PetBrain>();
        _host = builder.Build();
        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        try
        {
            var skinDirectory = Path.Combine(AppContext.BaseDirectory, "skins", DefaultSkinName);
            var skin = await _host.Services.GetRequiredService<ISkinLoader>().LoadAsync(skinDirectory);

            var petWindow = new PetWindow(
                skin,
                new SkinFrameCache(skin),
                _host.Services.GetRequiredService<PetBrain>(),
                behaviorOptions);
            petWindow.Show();
            _tray = new TrayIconService(petWindow, Shutdown);
        }
        catch (InvalidDataException ex)
        {
            logger.LogError(ex, "Failed to load the '{Skin}' skin.", DefaultSkinName);
            MessageBox.Show(ex.Message, "DeskBuddy", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        _instanceGuard?.Dispose();

        base.OnExit(e);
    }
}
