using System.IO;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Activity;
using DeskBuddy.Domain.Behavior;
using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.Pets;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.App.Tests;

/// <summary>
/// In-memory settings storage that can be made to fail.
/// </summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    /// <summary>
    /// Gets or sets the stored settings.
    /// </summary>
    public PetSettings Stored { get; set; } = PetSettings.Default;

    /// <summary>
    /// Gets or sets how many times <see cref="SaveAsync"/> succeeded.
    /// </summary>
    public int SaveCount { get; set; }

    /// <summary>
    /// Gets or sets an exception to throw from <see cref="LoadAsync"/>.
    /// </summary>
    public Exception? LoadException { get; set; }

    /// <summary>
    /// Gets or sets an exception to throw from <see cref="SaveAsync"/>.
    /// </summary>
    public Exception? SaveException { get; set; }

    /// <inheritdoc />
    public Task<PetSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        return LoadException is null ? Task.FromResult(Stored) : Task.FromException<PetSettings>(LoadException);
    }

    /// <summary>
    /// Gets or sets a task that <see cref="SaveAsync"/> waits for before it finishes, to hold a save in flight.
    /// </summary>
    public Task? SaveBlocker { get; set; }

    /// <summary>
    /// Gets or sets how many saves succeed before every later one fails with an <see cref="IOException"/>.
    /// </summary>
    public int? FailSavesAfter { get; set; }

    /// <inheritdoc />
    public async Task SaveAsync(PetSettings settings, CancellationToken cancellationToken = default)
    {
        if (FailSavesAfter is { } allowed && SaveCount >= allowed)
        {
            throw new IOException("disk full");
        }

        if (SaveBlocker is not null)
        {
            await SaveBlocker;
        }

        if (SaveException is not null)
        {
            throw SaveException;
        }

        SaveCount++;
        Stored = settings;
    }
}

/// <summary>
/// Hotkey registration stub that can be made to refuse shortcuts.
/// </summary>
internal sealed class FakeHotkeyService : IGlobalHotkeyService
{
    /// <inheritdoc />
    public event EventHandler? Pressed
    {
        add { }
        remove { }
    }

    /// <summary>
    /// Gets the shortcuts that were registered successfully, in order.
    /// </summary>
    public List<HotkeyGesture> Registrations { get; } = [];

    /// <summary>
    /// Gets or sets the shortcuts another program is said to use, which cannot be registered.
    /// </summary>
    public HashSet<HotkeyGesture> Taken { get; } = [];

    /// <inheritdoc />
    public bool TryRegister(HotkeyGesture gesture, out string? error)
    {
        if (Taken.Contains(gesture))
        {
            error = "already in use";
            return false;
        }

        Registrations.Add(gesture);
        error = null;
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>
/// Start-at-sign-in stub backed by a flag.
/// </summary>
internal sealed class FakeAutoStartService : IAutoStartService
{
    /// <summary>
    /// Gets or sets whether the application is registered to start at sign-in.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets why a change is refused; <see langword="null"/> lets changes through.
    /// </summary>
    public string? Refusal { get; set; }

    /// <inheritdoc />
    public bool IsEnabled()
    {
        return Enabled;
    }

    /// <inheritdoc />
    public bool TrySet(bool enabled, out string? error)
    {
        error = Refusal;
        if (Refusal is not null)
        {
            return false;
        }

        Enabled = enabled;
        return true;
    }
}

/// <summary>
/// Pet catalog stub with a list that tests can change.
/// </summary>
/// <param name="names">
/// The pet names to report at first.
/// </param>
internal sealed class FakePetCatalog(params string[] names) : IPetCatalog
{
    /// <summary>
    /// Gets or sets the names of the installed pets.
    /// </summary>
    public IReadOnlyList<string> Names { get; set; } = names;

    /// <inheritdoc />
    public IReadOnlyList<string> GetPetNames()
    {
        return Names;
    }
}

/// <summary>
/// Pet loader stub that loads pets named in advance and fails for any other.
/// </summary>
internal sealed class FakePetLoader : IPetLoader
{
    /// <summary>
    /// Gets the names of the pet folders that were asked for, in order.
    /// </summary>
    public List<string> Requested { get; } = [];

    /// <summary>
    /// Gets the names of the pets that load successfully.
    /// </summary>
    public HashSet<string> Available { get; } = [];

    /// <summary>
    /// Gets or sets a gate each load waits on, to hold a load back; <see langword="null"/> loads at once.
    /// </summary>
    public Dictionary<string, TaskCompletionSource> Gates { get; } = [];

    /// <summary>
    /// Gets the names pets give themselves, by folder name; a pet without an entry is named after its folder.
    /// </summary>
    public Dictionary<string, string> DisplayNames { get; } = [];

    /// <summary>
    /// Gets the states pets define besides Idle, by folder name.
    /// </summary>
    public Dictionary<string, PetState[]> ExtraStates { get; } = [];

    /// <inheritdoc />
    public async Task<Pet> LoadAsync(string petDirectory, CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(petDirectory);
        Requested.Add(name);

        if (Gates.TryGetValue(name, out var gate))
        {
            await gate.Task;
        }

        if (!Available.Contains(name))
        {
            throw new InvalidDataException($"Pet '{name}' is broken.");
        }

        return CreatePet(
            DisplayNames.GetValueOrDefault(name, name),
            ExtraStates.GetValueOrDefault(name, []));
    }

    /// <summary>
    /// Creates a minimal pet.
    /// </summary>
    /// <param name="name">
    /// The pet name.
    /// </param>
    /// <returns>
    /// A pet with a single idle frame.
    /// </returns>
    public static Pet CreatePet(string name)
    {
        return CreatePet(name, []);
    }

    /// <summary>
    /// Creates a pet that defines some states besides Idle.
    /// </summary>
    /// <param name="name">
    /// The pet name.
    /// </param>
    /// <param name="extraStates">
    /// The states to define besides Idle, each with two pictures.
    /// </param>
    /// <returns>
    /// A pet with an idle frame and two pictures for each extra state.
    /// </returns>
    public static Pet CreatePet(string name, IEnumerable<PetState> extraStates)
    {
        var animations = new Dictionary<PetState, PetAnimation>
        {
            [PetState.Idle] = new PetAnimation(4, ["idle.png"]),
        };
        foreach (var state in extraStates)
        {
            animations[state] = new PetAnimation(4, [$"{state}_0.png", $"{state}_1.png"]);
        }

        return new Pet(name, 128, 128, animations);
    }
}

/// <summary>
/// Builds the objects the settings tests need.
/// </summary>
internal static class TestBrain
{
    /// <summary>
    /// Creates a brain with quiet fake sensors.
    /// </summary>
    /// <returns>
    /// The brain.
    /// </returns>
    public static PetBrain Create()
    {
        var clock = new FakeTimeProvider();
        var behavior = new PetBehaviorOptions();
        return new PetBrain(
            new QuietIdle(),
            new QuietLoad(),
            new LoadReactionPolicy(new SystemLoadOptions(), clock),
            new QuietDetector(),
            new SedentaryReminderPolicy(new SedentaryOptions(), clock),
            new PetMessageQueue(TimeSpan.FromSeconds(30), clock),
            new PetBehaviorStateMachine(new PetBehaviorOptions(), clock, new Random(1)),
            new PetVisibility(clock, TimeSpan.FromSeconds(2)),
            new ChatterPolicy(clock, new Random(1)),
            new DevScenarios(
                new QuietForeground(),
                new QuietProcesses(),
                new DevActivityPolicy(behavior, clock),
                new BuildActivityPolicy(behavior, clock),
                new GitSyncPolicy(behavior, clock),
                new LateNightPolicy(behavior, clock),
                new ScenarioSwitches(),
                behavior),
            new SystemScenarios(
                new QuietForeground(),
                new QuietPower(),
                new MeetingPolicy(),
                new ChatPolicy(),
                new WelcomeBackPolicy(behavior),
                new PowerPolicy(behavior),
                new ScheduleClock(behavior, clock, new ScenarioSwitches { IsEnabled = false }),
                new ScenarioSwitches()));
    }

    /// <summary>
    /// Power stub reporting a computer without a battery.
    /// </summary>
    private sealed class QuietPower : IPowerStatusProvider
    {
        /// <inheritdoc />
        public PowerStatus GetStatus()
        {
            return default;
        }
    }

    /// <summary>
    /// Process list stub reporting no processes.
    /// </summary>
    private sealed class QuietProcesses : IProcessList
    {
        /// <inheritdoc />
        public IReadOnlySet<string> GetNames()
        {
            return new HashSet<string>();
        }
    }

    /// <summary>
    /// Foreground stub reporting no application.
    /// </summary>
    private sealed class QuietForeground : IForegroundAppDetector
    {
        /// <inheritdoc />
        public string? GetForegroundProcessName()
        {
            return null;
        }
    }

    /// <summary>
    /// Idle sensor stub reporting an active user.
    /// </summary>
    private sealed class QuietIdle : IIdleDetector
    {
        /// <inheritdoc />
        public TimeSpan GetIdleTime()
        {
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Load sensor stub reporting an idle machine.
    /// </summary>
    private sealed class QuietLoad : ISystemLoadMonitor
    {
        /// <inheritdoc />
        public SystemLoadSnapshot GetSnapshot()
        {
            return default;
        }
    }

    /// <summary>
    /// Detector stub reporting nothing special.
    /// </summary>
    private sealed class QuietDetector : IFullscreenDetector
    {
        /// <inheritdoc />
        public DoNotDisturbLevel GetLevel()
        {
            return DoNotDisturbLevel.None;
        }
    }
}

/// <summary>
/// A logger that remembers the levels it was asked to log at.
/// </summary>
/// <typeparam name="T">
/// The type the logger is for.
/// </typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    /// <summary>
    /// Gets the levels logged so far.
    /// </summary>
    public List<LogLevel> Levels { get; } = [];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Levels.Add(logLevel);
    }
}
