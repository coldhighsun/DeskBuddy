using System.IO;
using AwesomeAssertions;
using DeskBuddy.App.ViewModels;
using DeskBuddy.Application;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Scenarios;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="SettingsTabViewModel"/>.
/// </summary>
public class SettingsTabViewModelTests
{
    /// <summary>
    /// A shortcut other than the default one.
    /// </summary>
    private static readonly HotkeyGesture OtherKey = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B);

    /// <summary>
    /// The storage behind the settings.
    /// </summary>
    private readonly FakeSettingsStore _store = new();

    /// <summary>
    /// The system hotkey registration.
    /// </summary>
    private readonly FakeHotkeyService _hotkeys = new();

    /// <summary>
    /// The start-at-sign-in switch.
    /// </summary>
    private readonly FakeAutoStartService _autoStart = new();

    /// <summary>
    /// The settings service.
    /// </summary>
    private SettingsService _settings = null!;

    /// <summary>
    /// The view model under test, created by <see cref="CreateAsync"/>.
    /// </summary>
    private SettingsTabViewModel _viewModel = null!;

    /// <summary>
    /// Loads the settings and creates the view model over fakes.
    /// </summary>
    /// <returns>
    /// A task that completes when the view model is ready.
    /// </returns>
    private async Task CreateAsync()
    {
        _settings = new SettingsService(_store);
        await _settings.InitializeAsync();

        var bossKey = new BossKeyController(_settings, _hotkeys, TestBrain.Create());
        _viewModel = new SettingsTabViewModel(
            _settings,
            bossKey,
            _autoStart,
            () => LogDirectoryOpened++);
    }

    /// <summary>
    /// The log button asks the host to open the log directory.
    /// </summary>
    [Fact]
    public async Task OpenLogsCommand_Executed_OpensTheLogDirectory()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.OpenLogsCommand.Execute(null);

        // Assert
        LogDirectoryOpened.Should().Be(1);
    }

    /// <summary>
    /// Gets how many times the log directory was opened.
    /// </summary>
    private int LogDirectoryOpened { get; set; }

    /// <summary>
    /// Waits, briefly, for something that happens after a hand-off to another continuation.
    /// </summary>
    /// <param name="condition">
    /// The condition to wait for.
    /// </param>
    /// <returns>
    /// A task that completes when the condition holds, or after a timeout.
    /// </returns>
    private static async Task EventuallyAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// The tab starts out showing the saved settings.
    /// </summary>
    [Fact]
    public async Task Constructor_ShowsCurrentSettings()
    {
        // Arrange
        _store.Stored = PetSettings.Default with
        {
            Mode = DisturbanceMode.Roam,
            SedentaryReminderAfter = TimeSpan.FromMinutes(60),
            SleepAfter = TimeSpan.FromMinutes(8),
            AutoHideWhenFullscreen = false,
            DetectCoveredMonitor = true,
            BlacklistedProcesses = ["zoom", "teams"],
        };

        // Act
        await CreateAsync();

        // Assert
        _viewModel.IsRoam.Should().BeTrue();
        _viewModel.IsDocked.Should().BeFalse();
        _viewModel.SedentaryText.Should().Be("60");
        _viewModel.SleepText.Should().Be("8");
        _viewModel.BossKeyText.Should().Be("Ctrl+Alt+H");
        _viewModel.AutoHideWhenFullscreen.Should().BeFalse();
        _viewModel.DetectCoveredMonitor.Should().BeTrue();
        _viewModel.BlacklistText.Should().Be($"zoom{Environment.NewLine}teams");
        _viewModel.IsEditable.Should().BeTrue();
    }

    /// <summary>
    /// A valid number of minutes is applied and saved, and each entry changes its own setting.
    /// </summary>
    [Fact]
    public async Task Minutes_ValidValues_AreAppliedAndSaved()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.SedentaryText = "90";
        _viewModel.SleepText = "12";

        // Assert
        _settings.Current.SedentaryReminderAfter.Should().Be(TimeSpan.FromMinutes(90));
        _settings.Current.SleepAfter.Should().Be(TimeSpan.FromMinutes(12));
        _store.Stored.Should().Be(_settings.Current);
        _viewModel.TimingError.Should().BeEmpty();
    }

    /// <summary>
    /// Spaces around the number are ignored.
    /// </summary>
    [Fact]
    public async Task Minutes_WithSurroundingSpaces_AreAccepted()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.SedentaryText = "  30 ";

        // Assert
        _settings.Current.SedentaryReminderAfter.Should().Be(TimeSpan.FromMinutes(30));
        _viewModel.SedentaryText.Should().Be("30");
    }

    /// <summary>
    /// A value that is not valid is not applied or saved, and the message says what is expected.
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("2.5")]
    [InlineData("481")]
    [InlineData("99999999999")]
    public async Task Minutes_InvalidValue_IsNotAppliedAndExplained(string text)
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.SedentaryText = text;

        // Assert
        _settings.Current.SedentaryReminderAfter.Should().Be(PetSettings.Default.SedentaryReminderAfter);
        _store.SaveCount.Should().Be(0);
        _viewModel.TimingError.Should().Contain("1").And.Contain("480");
        _viewModel.SedentaryText.Should().Be(text);
    }

    /// <summary>
    /// The upper limits of the entries are accepted and the next value up is not.
    /// </summary>
    [Theory]
    [InlineData(nameof(SettingsTabViewModel.SedentaryText), 480)]
    [InlineData(nameof(SettingsTabViewModel.SleepText), 720)]
    public async Task Minutes_AtUpperLimit_AcceptedAndAboveRejected(string property, int limit)
    {
        // Arrange
        await CreateAsync();
        var setter = typeof(SettingsTabViewModel).GetProperty(property)!;

        // Act
        setter.SetValue(_viewModel, limit.ToString());
        var accepted = _viewModel.TimingError;
        setter.SetValue(_viewModel, (limit + 1).ToString());

        // Assert
        accepted.Should().BeEmpty();
        _viewModel.TimingError.Should().NotBeEmpty();
        _store.SaveCount.Should().Be(1);
    }

    /// <summary>
    /// A later valid entry clears the message left by an invalid one.
    /// </summary>
    [Fact]
    public async Task Minutes_ValidAfterInvalid_ClearsError()
    {
        // Arrange
        await CreateAsync();
        _viewModel.SleepText = "abc";

        // Act
        _viewModel.SleepText = "10";

        // Assert
        _viewModel.TimingError.Should().BeEmpty();
    }

    /// <summary>
    /// Choosing a mode applies it; the radio button that is switched off does not change anything.
    /// </summary>
    [Fact]
    public async Task Mode_ChoosingRoam_AppliesRoamOnly()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.IsRoam = true;

        // Assert
        _settings.Current.Mode.Should().Be(DisturbanceMode.Roam);
        _viewModel.IsDocked.Should().BeFalse();
        _store.SaveCount.Should().Be(1);
    }

    /// <summary>
    /// Choosing the docked mode again applies it.
    /// </summary>
    [Fact]
    public async Task Mode_ChoosingDockedAfterRoam_AppliesDocked()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { Mode = DisturbanceMode.Roam };
        await CreateAsync();

        // Act
        _viewModel.IsDocked = true;

        // Assert
        _settings.Current.Mode.Should().Be(DisturbanceMode.Docked);
    }

    /// <summary>
    /// The two full-screen choices are applied.
    /// </summary>
    [Fact]
    public async Task FullscreenChoices_AreApplied()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.AutoHideWhenFullscreen = false;
        _viewModel.DetectCoveredMonitor = true;

        // Assert
        _settings.Current.AutoHideWhenFullscreen.Should().BeFalse();
        _settings.Current.DetectCoveredMonitor.Should().BeTrue();
    }

    /// <summary>
    /// The process list is split into lines, cleaned up, and shown back in its cleaned-up form.
    /// </summary>
    [Fact]
    public async Task Blacklist_Edited_IsCleanedAndShownBack()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.BlacklistText = $"Zoom.exe\r\n  teams \n\n zoom\r\n";

        // Assert
        _settings.Current.BlacklistedProcesses.Should().Equal("Zoom", "teams");
        _viewModel.BlacklistText.Should().Be($"Zoom{Environment.NewLine}teams");
    }

    /// <summary>
    /// The tab shows the development detection switch and tool list from the saved settings.
    /// </summary>
    [Fact]
    public async Task Constructor_ShowsDevelopmentSettings()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { DevModeEnabled = false, DevProcesses = ["Code", "zed"] };

        // Act
        await CreateAsync();

        // Assert
        _viewModel.DevModeEnabled.Should().BeFalse();
        _viewModel.DevProcessesText.Should().Be($"Code{Environment.NewLine}zed");
    }

    /// <summary>
    /// Switching development detection off is applied to the settings.
    /// </summary>
    [Fact]
    public async Task DevMode_Toggled_IsApplied()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.DevModeEnabled = false;

        // Assert
        _settings.Current.DevModeEnabled.Should().BeFalse();
    }

    /// <summary>
    /// The tool list is split into lines, cleaned up, and shown back in its cleaned-up form.
    /// </summary>
    [Fact]
    public async Task DevProcesses_Edited_IsCleanedAndShownBack()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.DevProcessesText = $"Code.exe\r\n  zed \n\n code\r\n";

        // Assert
        _settings.Current.DevProcesses.Should().Equal("Code", "zed");
        _viewModel.DevProcessesText.Should().Be($"Code{Environment.NewLine}zed");
    }

    /// <summary>
    /// The tab lists every scenario and shows which ones are switched off.
    /// </summary>
    [Fact]
    public async Task Constructor_ShowsScenarioSwitches()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { ScenariosEnabled = false, DisabledScenarios = ["BuildDone"] };

        // Act
        await CreateAsync();

        // Assert
        _viewModel.ScenariosEnabled.Should().BeFalse();
        _viewModel.Scenarios.Select(toggle => toggle.State).Should().Equal(ScenarioCatalog.All);
        _viewModel.Scenarios.Single(toggle => toggle.State == PetState.BuildDone).IsEnabled.Should().BeFalse();
        _viewModel.Scenarios.Single(toggle => toggle.State == PetState.Building).IsEnabled.Should().BeTrue();
    }

    /// <summary>
    /// Switching a scenario off adds it to the list of disabled ones, switching it on takes it off again.
    /// </summary>
    [Fact]
    public async Task ScenarioToggle_Flipped_UpdatesDisabledList()
    {
        // Arrange
        await CreateAsync();
        var toggle = _viewModel.Scenarios.Single(item => item.State == PetState.GitPush);

        // Act
        toggle.IsEnabled = false;
        var afterOff = _settings.Current.DisabledScenarios.ToArray();
        toggle.IsEnabled = true;

        // Assert
        afterOff.Should().BeEquivalentTo("Chime", "GitPush");
        _settings.Current.DisabledScenarios.Should().Equal("Chime");
    }

    /// <summary>
    /// The chosen language is stored, and the settings are shown with the language they hold.
    /// </summary>
    [Fact]
    public async Task SelectedLanguage_Changed_IsStored()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { Language = AppLanguage.ZhCN };
        await CreateAsync();
        var shownAtStart = _viewModel.SelectedLanguage;

        // Act
        _viewModel.SelectedLanguage = AppLanguage.En;

        // Assert
        shownAtStart.Should().Be(AppLanguage.ZhCN);
        _settings.Current.Language.Should().Be(AppLanguage.En);
        _viewModel.Languages.Select(option => option.Value).Should().Equal(AppLanguage.Auto, AppLanguage.ZhCN, AppLanguage.En);
    }

    /// <summary>
    /// The chosen theme is stored, and the settings are shown with the theme they hold.
    /// </summary>
    [Fact]
    public async Task SelectedTheme_Changed_IsStored()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { Theme = AppTheme.Light };
        await CreateAsync();
        var shownAtStart = _viewModel.SelectedTheme;

        // Act
        _viewModel.SelectedTheme = AppTheme.Dark;

        // Assert
        shownAtStart.Should().Be(AppTheme.Light);
        _settings.Current.Theme.Should().Be(AppTheme.Dark);
        _viewModel.Themes.Select(option => option.Value).Should().Equal(AppTheme.Auto, AppTheme.Light, AppTheme.Dark);
    }

    /// <summary>
    /// The chosen pet size is stored, and the settings are shown with the size they hold.
    /// </summary>
    [Fact]
    public async Task SelectedSize_Changed_IsStored()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { Size = PetSize.Large };
        await CreateAsync();
        var shownAtStart = _viewModel.SelectedSize;

        // Act
        _viewModel.SelectedSize = PetSize.Small;

        // Assert
        shownAtStart.Should().Be(PetSize.Large);
        _settings.Current.Size.Should().Be(PetSize.Small);
        _viewModel.Sizes.Select(option => option.Value).Should().Equal(PetSize.Small, PetSize.Medium, PetSize.Large);
    }

    /// <summary>
    /// The choice to let the pet climb along the edges of the screen is stored, and shown from the stored settings.
    /// </summary>
    [Fact]
    public async Task ClimbEdges_Changed_IsStored()
    {
        // Arrange
        await CreateAsync();
        var shownAtStart = _viewModel.ClimbEdges;

        // Act
        _viewModel.ClimbEdges = true;

        // Assert
        shownAtStart.Should().BeFalse();
        _settings.Current.ClimbEdges.Should().BeTrue();
    }

    /// <summary>
    /// An edited end of the working day is applied; text that is no time of day is ignored.
    /// </summary>
    [Fact]
    public async Task ClockOutTime_Edited_IsAppliedWhenValid()
    {
        // Arrange
        await CreateAsync();
        var shownAtStart = _viewModel.ClockOutTimeText;

        // Act
        _viewModel.ClockOutTimeText = "17:30";
        var valid = _settings.Current.ClockOutTime;
        _viewModel.ClockOutTimeText = "soon";
        var afterGarbage = _settings.Current.ClockOutTime;
        _viewModel.ClockOutTimeText = "25:00";
        var afterOutOfRange = _settings.Current.ClockOutTime;

        // Assert
        shownAtStart.Should().Be("18:00");
        valid.Should().Be(new TimeSpan(17, 30, 0));
        afterGarbage.Should().Be(valid);
        afterOutOfRange.Should().Be(valid);
    }

    /// <summary>
    /// Text that is no time of day is explained, and the explanation goes away once the text is a time.
    /// </summary>
    [Fact]
    public async Task ClockOutTime_Invalid_ShowsAnErrorUntilFixed()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ClockOutTimeText = "6pm";
        var afterGarbage = _viewModel.ClockOutTimeError;
        _viewModel.ClockOutTimeText = "17:45";
        var afterFix = _viewModel.ClockOutTimeError;

        // Assert
        afterGarbage.Should().NotBeEmpty();
        afterFix.Should().BeEmpty();
    }

    /// <summary>
    /// The list of meeting applications is shown, and an edit is split into lines, cleaned up and shown back.
    /// </summary>
    [Fact]
    public async Task MeetingProcesses_Edited_IsCleanedAndShownBack()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { MeetingProcesses = ["Zoom"] };
        await CreateAsync();
        var shownAtStart = _viewModel.MeetingProcessesText;

        // Act
        _viewModel.MeetingProcessesText = "Zoom.exe\r\n  Slack \n\n slack\r\n";

        // Assert
        shownAtStart.Should().Be("Zoom");
        _settings.Current.MeetingProcesses.Should().Equal("Zoom", "Slack");
        _viewModel.MeetingProcessesText.Should().Be($"Zoom{Environment.NewLine}Slack");
    }

    /// <summary>
    /// The master switch for scenarios is applied to the settings.
    /// </summary>
    [Fact]
    public async Task ScenariosEnabled_Toggled_IsApplied()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ScenariosEnabled = false;

        // Assert
        _settings.Current.ScenariosEnabled.Should().BeFalse();
    }

    /// <summary>
    /// Switching start at sign-in on is passed to the system.
    /// </summary>
    [Fact]
    public async Task AutoStart_SwitchedOn_IsPassedToSystem()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.AutoStart = true;

        // Assert
        _autoStart.Enabled.Should().BeTrue();
        _viewModel.AutoStartError.Should().BeEmpty();
    }

    /// <summary>
    /// If the system refuses, the switch goes back to off and the reason is shown.
    /// </summary>
    [Fact]
    public async Task AutoStart_Refused_SwitchGoesBackWithMessage()
    {
        // Arrange
        await CreateAsync();
        _autoStart.Refusal = "policy forbids it";

        // Act
        _viewModel.AutoStart = true;

        // Assert
        _viewModel.AutoStart.Should().BeFalse();
        _autoStart.Enabled.Should().BeFalse();
        _viewModel.AutoStartError.Should().Contain("policy forbids it");
    }

    /// <summary>
    /// A change made outside the application, such as disabling the entry in Task Manager, shows up when the tab is
    /// refreshed.
    /// </summary>
    [Fact]
    public async Task RefreshExternalState_AutoStartChangedElsewhere_IsShown()
    {
        // Arrange
        await CreateAsync();
        _autoStart.Enabled = true;

        // Act
        _viewModel.RefreshExternalState();

        // Assert
        _viewModel.AutoStart.Should().BeTrue();
    }

    /// <summary>
    /// Refreshing from outside does not count as the user's edit and saves nothing.
    /// </summary>
    [Fact]
    public async Task RefreshExternalState_DoesNotSave()
    {
        // Arrange
        await CreateAsync();
        _autoStart.Enabled = true;

        // Act
        _viewModel.RefreshExternalState();

        // Assert
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// A failed save is shown, and the change stays in effect in memory.
    /// </summary>
    [Fact]
    public async Task Save_Fails_ShowsErrorAndKeepsChange()
    {
        // Arrange
        await CreateAsync();
        _store.SaveException = new IOException("disk full");

        // Act
        _viewModel.IsRoam = true;

        // Assert
        _viewModel.SaveError.Should().Contain("disk full");
        _settings.Current.Mode.Should().Be(DisturbanceMode.Roam);
    }

    /// <summary>
    /// A later successful save clears the message.
    /// </summary>
    [Fact]
    public async Task Save_SucceedsAfterFailure_ClearsError()
    {
        // Arrange
        await CreateAsync();
        _store.SaveException = new IOException("disk full");
        _viewModel.IsRoam = true;
        _store.SaveException = null;

        // Act
        _viewModel.SleepText = "10";

        // Assert
        _viewModel.SaveError.Should().BeEmpty();
    }

    /// <summary>
    /// When the saved settings could not be read the tab cannot be edited and says why.
    /// </summary>
    [Fact]
    public async Task Constructor_LoadFailed_IsNotEditableAndExplains()
    {
        // Arrange
        _store.LoadException = new IOException("locked");

        // Act
        await CreateAsync();

        // Assert
        _viewModel.IsEditable.Should().BeFalse();
        _viewModel.LoadFailureMessage.Should().Contain("locked");
    }

    /// <summary>
    /// Nothing is saved when the settings could not be read, even if an edit sneaks through.
    /// </summary>
    [Fact]
    public async Task Edit_AfterLoadFailure_NeverSaves()
    {
        // Arrange
        _store.LoadException = new IOException("locked");
        await CreateAsync();

        // Act
        _viewModel.IsRoam = true;

        // Assert
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// A new boss key that can be registered is applied and saved.
    /// </summary>
    [Fact]
    public async Task ApplyBossKey_Registrable_IsAppliedAndSaved()
    {
        // Arrange
        await CreateAsync();

        // Act
        await _viewModel.ApplyBossKeyAsync(OtherKey);

        // Assert
        _settings.Current.BossKey.Should().Be(OtherKey);
        _viewModel.BossKeyText.Should().Be("Ctrl+Shift+F12");
        _viewModel.BossKeyError.Should().BeEmpty();
        _hotkeys.Registrations.Should().Contain(OtherKey);
    }

    /// <summary>
    /// A boss key another program already uses is not kept: the setting goes back to the working shortcut and the
    /// reason is shown.
    /// </summary>
    [Fact]
    public async Task ApplyBossKey_Taken_RevertsToWorkingKeyAndExplains()
    {
        // Arrange
        await CreateAsync();
        _hotkeys.Taken.Add(OtherKey);

        // Act
        await _viewModel.ApplyBossKeyAsync(OtherKey);

        // Assert
        _settings.Current.BossKey.Should().Be(PetSettings.Default.BossKey);
        _store.Stored.BossKey.Should().Be(PetSettings.Default.BossKey);
        _viewModel.BossKeyText.Should().Be("Ctrl+Alt+H");
        _viewModel.BossKeyError.Should().Contain("Ctrl+Shift+F12").And.Contain("already in use");
    }

    /// <summary>
    /// A later working boss key clears the earlier message.
    /// </summary>
    [Fact]
    public async Task ApplyBossKey_WorkingAfterTaken_ClearsMessage()
    {
        // Arrange
        await CreateAsync();
        _hotkeys.Taken.Add(OtherKey);
        await _viewModel.ApplyBossKeyAsync(OtherKey);
        var another = new HotkeyGesture(HotkeyModifiers.Alt, 0x71);

        // Act
        await _viewModel.ApplyBossKeyAsync(another);

        // Assert
        _viewModel.BossKeyError.Should().BeEmpty();
        _settings.Current.BossKey.Should().Be(another);
    }

    /// <summary>
    /// Entering the shortcut that is already set changes nothing.
    /// </summary>
    [Fact]
    public async Task ApplyBossKey_SameKey_DoesNothing()
    {
        // Arrange
        await CreateAsync();

        // Act
        await _viewModel.ApplyBossKeyAsync(PetSettings.Default.BossKey);

        // Assert
        _store.SaveCount.Should().Be(0);
        _hotkeys.Registrations.Should().HaveCount(1);
    }

    /// <summary>
    /// Keys that cannot be a boss key are explained.
    /// </summary>
    [Fact]
    public async Task ReportBossKeyRejected_ShowsMessage()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ReportBossKeyRejected();

        // Assert
        _viewModel.BossKeyError.Should().NotBeEmpty();
    }

    /// <summary>
    /// If the boss key from the settings could not be registered at startup the tab says so at once.
    /// </summary>
    [Fact]
    public async Task Constructor_BossKeyTakenAtStartup_ShowsMessage()
    {
        // Arrange
        _hotkeys.Taken.Add(PetSettings.Default.BossKey);

        // Act
        await CreateAsync();

        // Assert
        _viewModel.BossKeyError.Should().Contain("Ctrl+Alt+H").And.Contain("already in use");
    }
}
