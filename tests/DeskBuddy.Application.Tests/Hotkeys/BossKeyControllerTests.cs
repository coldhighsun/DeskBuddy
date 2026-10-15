using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application.Tests.Hotkeys;

/// <summary>
/// Tests for <see cref="BossKeyController"/>.
/// </summary>
public class BossKeyControllerTests
{
    /// <summary>
    /// A shortcut other than the default, for changing the setting.
    /// </summary>
    private static readonly HotkeyGesture OtherKey = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B);

    /// <summary>
    /// The fake system hotkey registration.
    /// </summary>
    private readonly FakeHotkeyService _hotkeys = new();

    /// <summary>
    /// The settings the controller follows.
    /// </summary>
    private readonly SettingsService _settings = new(new NullStore());

    /// <summary>
    /// The brain whose visibility the shortcut toggles.
    /// </summary>
    private readonly PetBrain _brain;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossKeyControllerTests"/> class.
    /// </summary>
    public BossKeyControllerTests()
    {
        var clock = new FakeTimeProvider();
        var behavior = new PetBehaviorOptions();
        _brain = new PetBrain(
            new FakeIdle(),
            new FakeLoad(),
            new LoadReactionPolicy(new SystemLoadOptions(), clock),
            new FakeDetector(),
            new SedentaryReminderPolicy(new SedentaryOptions(), clock),
            new PetMessageQueue(TimeSpan.FromSeconds(30), clock),
            new PetBehaviorStateMachine(new PetBehaviorOptions(), clock, new Random(1)),
            new PetVisibility(clock, TimeSpan.FromSeconds(2)),
            new ChatterPolicy(clock, new Random(1)),
            new DevScenarios(
                new FakeForegroundApp(),
                new FakeProcessList(),
                new DevActivityPolicy(behavior, clock),
                new BuildActivityPolicy(behavior, clock),
                new GitSyncPolicy(behavior, clock),
                new LateNightPolicy(behavior, clock),
                new ScenarioSwitches(),
                behavior),
            new SystemScenarios(
                new FakeForegroundApp(),
                new FakePowerStatusProvider(),
                new MeetingPolicy(),
                new ChatPolicy(),
                new WelcomeBackPolicy(behavior),
                new PowerPolicy(behavior),
                new ScheduleClock(behavior, clock, new ScenarioSwitches { IsEnabled = false }),
                new ScenarioSwitches()));
    }

    /// <summary>
    /// Creating the controller registers the shortcut from the settings.
    /// </summary>
    [Fact]
    public void Constructor_RegistersShortcutFromSettings()
    {
        // Act
        using var controller = new BossKeyController(_settings, _hotkeys, _brain);

        // Assert
        _hotkeys.Registrations.Should().Equal(PetSettings.Default.BossKey);
        controller.Registered.Should().Be(PetSettings.Default.BossKey);
        controller.RegistrationError.Should().BeNull();
    }

    /// <summary>
    /// Pressing the shortcut hides the pet, and pressing it again shows it.
    /// </summary>
    [Fact]
    public void Pressed_Twice_HidesThenShowsPet()
    {
        // Arrange
        using var controller = new BossKeyController(_settings, _hotkeys, _brain);

        // Act
        _hotkeys.Press();
        var hidden = _brain.Visibility.IsManuallyHidden;
        _hotkeys.Press();

        // Assert
        hidden.Should().BeTrue();
        _brain.Visibility.IsManuallyHidden.Should().BeFalse();
    }

    /// <summary>
    /// Changing the shortcut in the settings registers the new one.
    /// </summary>
    [Fact]
    public async Task SettingsChanged_NewBossKey_IsRegistered()
    {
        // Arrange
        using var controller = new BossKeyController(_settings, _hotkeys, _brain);

        // Act
        await _settings.UpdateAsync(s => s with { BossKey = OtherKey }, TestContext.Current.CancellationToken);

        // Assert
        _hotkeys.Registrations.Should().Equal(PetSettings.Default.BossKey, OtherKey);
        controller.Registered.Should().Be(OtherKey);
    }

    /// <summary>
    /// Changing an unrelated setting does not register anything again.
    /// </summary>
    [Fact]
    public async Task SettingsChanged_SameBossKey_DoesNotRegisterAgain()
    {
        // Arrange
        using var controller = new BossKeyController(_settings, _hotkeys, _brain);

        // Act
        await _settings.UpdateAsync(s => s with { ClimbEdges = true }, TestContext.Current.CancellationToken);

        // Assert
        _hotkeys.Registrations.Should().HaveCount(1);
    }

    /// <summary>
    /// A shortcut another program already uses is reported, not thrown, and the application carries on.
    /// </summary>
    [Fact]
    public void Constructor_ShortcutTaken_RecordsErrorAndHasNoRegistration()
    {
        // Arrange
        _hotkeys.Error = "already registered";

        // Act
        using var controller = new BossKeyController(_settings, _hotkeys, _brain);

        // Assert
        controller.Registered.Should().BeNull();
        controller.RegistrationError.Should().Be("already registered");
    }

    /// <summary>
    /// When the new shortcut cannot be registered the previous one stays registered and the error is reported.
    /// </summary>
    [Fact]
    public async Task SettingsChanged_NewShortcutTaken_KeepsPreviousAndReportsError()
    {
        // Arrange
        using var controller = new BossKeyController(_settings, _hotkeys, _brain);
        var raised = 0;
        controller.StatusChanged += (_, _) => raised++;
        _hotkeys.Error = "already registered";

        // Act
        await _settings.UpdateAsync(s => s with { BossKey = OtherKey }, TestContext.Current.CancellationToken);

        // Assert
        controller.Registered.Should().Be(PetSettings.Default.BossKey);
        controller.RegistrationError.Should().Be("already registered");
        raised.Should().Be(1);
    }

    /// <summary>
    /// Going back to the shortcut that is registered clears the error without registering again.
    /// </summary>
    [Fact]
    public async Task SettingsChanged_BackToRegisteredShortcut_ClearsError()
    {
        // Arrange
        using var controller = new BossKeyController(_settings, _hotkeys, _brain);
        _hotkeys.Error = "already registered";
        await _settings.UpdateAsync(s => s with { BossKey = OtherKey }, TestContext.Current.CancellationToken);
        _hotkeys.Error = null;

        // Act
        await _settings.UpdateAsync(s => s with { BossKey = PetSettings.Default.BossKey }, TestContext.Current.CancellationToken);

        // Assert
        controller.RegistrationError.Should().BeNull();
        _hotkeys.Registrations.Should().HaveCount(1);
    }

    /// <summary>
    /// After disposal the shortcut no longer hides the pet.
    /// </summary>
    [Fact]
    public void Dispose_ThenPressed_DoesNothing()
    {
        // Arrange
        var controller = new BossKeyController(_settings, _hotkeys, _brain);
        controller.Dispose();

        // Act
        _hotkeys.Press();

        // Assert
        _brain.Visibility.IsManuallyHidden.Should().BeFalse();
    }

    /// <summary>
    /// Hotkey service stub that records registrations and can be made to fail.
    /// </summary>
    private sealed class FakeHotkeyService : IGlobalHotkeyService
    {
        /// <inheritdoc />
        public event EventHandler? Pressed;

        /// <summary>
        /// Gets the shortcuts that were registered successfully, in order.
        /// </summary>
        public List<HotkeyGesture> Registrations { get; } = [];

        /// <summary>
        /// Gets or sets the error to fail with; <see langword="null"/> lets registration succeed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Simulates the user pressing the registered shortcut.
        /// </summary>
        public void Press()
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public bool TryRegister(HotkeyGesture gesture, out string? error)
        {
            error = Error;
            if (Error is not null)
            {
                return false;
            }

            Registrations.Add(gesture);
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Settings storage that keeps nothing.
    /// </summary>
    private sealed class NullStore : ISettingsStore
    {
        /// <inheritdoc />
        public Task<PetSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PetSettings.Default);
        }

        /// <inheritdoc />
        public Task SaveAsync(PetSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Idle sensor stub reporting an active user.
    /// </summary>
    private sealed class FakeIdle : IIdleDetector
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
    private sealed class FakeLoad : ISystemLoadMonitor
    {
        /// <inheritdoc />
        public SystemLoadSnapshot GetSnapshot()
        {
            return default;
        }
    }

    /// <summary>
    /// Detector stub reporting that nothing needs the pet to stay away.
    /// </summary>
    private sealed class FakeDetector : IFullscreenDetector
    {
        /// <inheritdoc />
        public DoNotDisturbLevel GetLevel()
        {
            return DoNotDisturbLevel.None;
        }
    }
}
