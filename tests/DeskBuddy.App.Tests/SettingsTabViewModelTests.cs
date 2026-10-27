using System.IO;
using System.Windows.Media;
using DeskBuddy.App.ViewModels;
using DeskBuddy.Domain.Scenarios;

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
    /// A picture for the pets to show; its content does not matter to these tests.
    /// </summary>
    private static readonly ImageSource TestPicture = new DrawingImage();

    /// <summary>
    /// Waits until a condition holds, for updates that finish on another thread after a held save is released; gives up
    /// after a few seconds, leaving the assertions to say what is wrong.
    /// </summary>
    /// <param name="condition">
    /// The condition to wait for.
    /// </param>
    /// <returns>
    /// A task that completes when the condition holds or the time is up.
    /// </returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Loads the settings and creates the view model over fakes.
    /// </summary>
    /// <param name="pets">
    /// The installed pets the sizes are drawn with, or <see langword="null"/> for none.
    /// </param>
    /// <returns>
    /// A task that completes when the view model is ready.
    /// </returns>
    private async Task CreateAsync(InstalledPets? pets = null)
    {
        _settings = new SettingsService(_store);
        await _settings.InitializeAsync();

        var bossKey = new BossKeyController(_settings, _hotkeys, TestBrain.Create());
        _viewModel = new SettingsTabViewModel(
            _settings,
            bossKey,
            _autoStart,
            () => LogDirectoryOpened++,
            pets);
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
            ChatterEnabled = true,
            SedentaryReminderAfter = TimeSpan.FromMinutes(60),
            SleepAfter = TimeSpan.FromMinutes(8),
            AutoHideWhenFullscreen = false,
            DetectCoveredMonitor = true,
            BlacklistedProcesses = ["game", "teams"],
        };

        // Act
        await CreateAsync();

        // Assert
        _viewModel.ChatterEnabled.Should().BeTrue();
        _viewModel.SedentaryText.Should().Be("60");
        _viewModel.SleepText.Should().Be("8");
        _viewModel.BossKeyText.Should().Be("Ctrl+Alt+H");
        _viewModel.AutoHideWhenFullscreen.Should().BeFalse();
        _viewModel.DetectCoveredMonitor.Should().BeTrue();
        _viewModel.BlacklistText.Should().Be($"game{Environment.NewLine}teams");
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
        await _viewModel.TrySaveAsync();

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
        await _viewModel.TrySaveAsync();

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
    /// The largest number of minutes is accepted and one more is refused, which also blocks saving.
    /// </summary>
    /// <param name="property">
    /// The property to type into.
    /// </param>
    /// <param name="limit">
    /// The largest accepted number of minutes.
    /// </param>
    /// <returns>
    /// A task that completes when the test is done.
    /// </returns>
    [Theory]
    [InlineData("SedentaryText", 480)]
    [InlineData("SleepText", 720)]
    public async Task Minutes_AtUpperLimit_AcceptedAndAboveRejected(string property, int limit)
    {
        // Arrange
        await CreateAsync();
        var setter = typeof(SettingsTabViewModel).GetProperty(property)!;

        // Act
        setter.SetValue(_viewModel, limit.ToString());
        var accepted = _viewModel.TimingError;
        await _viewModel.TrySaveAsync();
        setter.SetValue(_viewModel, (limit + 1).ToString());

        // Assert
        accepted.Should().BeEmpty();
        _viewModel.TimingError.Should().NotBeEmpty();
        _viewModel.CanSave.Should().BeFalse();
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
    /// Switching the casual remarks on applies and saves the choice.
    /// </summary>
    [Fact]
    public async Task ChatterEnabled_SwitchedOn_IsAppliedAndSaved()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ChatterEnabled = true;
        await _viewModel.TrySaveAsync();

        // Assert
        _settings.Current.ChatterEnabled.Should().BeTrue();
        _store.SaveCount.Should().Be(1);
    }

    /// <summary>
    /// Switching the casual remarks off again applies the choice.
    /// </summary>
    [Fact]
    public async Task ChatterEnabled_SwitchedOffAgain_IsApplied()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { ChatterEnabled = true };
        await CreateAsync();

        // Act
        _viewModel.ChatterEnabled = false;
        await _viewModel.TrySaveAsync();

        // Assert
        _settings.Current.ChatterEnabled.Should().BeFalse();
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
        await _viewModel.TrySaveAsync();

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
        _viewModel.BlacklistText = $"Game.exe\r\n  teams \n\n game\r\n";
        await _viewModel.TrySaveAsync();

        // Assert
        _settings.Current.BlacklistedProcesses.Should().Equal("Game", "teams");
        _viewModel.BlacklistText.Should().Be($"Game{Environment.NewLine}teams");
    }

    /// <summary>
    /// The tab shows the development tool list from the saved settings.
    /// </summary>
    [Fact]
    public async Task Constructor_ShowsDevelopmentSettings()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { DevProcesses = ["Code", "zed"] };

        // Act
        await CreateAsync();

        // Assert
        _viewModel.DevProcessesText.Should().Be($"Code{Environment.NewLine}zed");
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
        await _viewModel.TrySaveAsync();

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
    /// A scenario switch changes the list of disabled scenarios once saved, and switching back takes it out again.
    /// </summary>
    [Fact]
    public async Task ScenarioToggle_Flipped_UpdatesDisabledList()
    {
        // Arrange
        await CreateAsync();
        var toggle = _viewModel.Scenarios.Single(item => item.State == PetState.GitPush);

        // Act
        toggle.IsEnabled = false;
        await _viewModel.TrySaveAsync();
        var afterOff = _settings.Current.DisabledScenarios.ToArray();
        toggle.IsEnabled = true;
        await _viewModel.TrySaveAsync();

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
        await _viewModel.TrySaveAsync();

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
        await _viewModel.TrySaveAsync();

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
        await _viewModel.TrySaveAsync();

        // Assert
        shownAtStart.Should().Be(PetSize.Large);
        _settings.Current.Size.Should().Be(PetSize.Small);
        _viewModel.SizeChoices.Select(choice => choice.Size).Should().Equal(PetSize.Small, PetSize.Medium, PetSize.Large);
    }

    /// <summary>
    /// Without installed pets the sizes are still offered, by name only.
    /// </summary>
    [Fact]
    public async Task SizeChoices_NoPets_AreOfferedWithoutPictures()
    {
        // Arrange
        await CreateAsync();

        // Assert
        _viewModel.SizeChoices.Should().HaveCount(3);
        _viewModel.SizeChoices.Should().OnlyContain(choice => choice.Picture == null && choice.Width == 0);
    }

    /// <summary>
    /// The sizes show the pet in use at the size each gives it, and follow the pet when another one is chosen.
    /// </summary>
    [Fact]
    public async Task SizeChoices_PetChosen_ShowThatPetAtEachSize()
    {
        // Arrange
        var catalog = new FakePetCatalog("cat", "mouse");
        var loader = new FakePetLoader();
        loader.Available.Add("cat");
        loader.Available.Add("mouse");
        var pets = new InstalledPets(catalog, loader, "pets", _ => TestPicture);
        await pets.RefreshAsync();
        await CreateAsync(pets);

        // Act
        var forDefaultPet = _viewModel.SizeChoices.Select(choice => choice.Width).ToList();
        await _settings.UpdateAsync(settings => settings with { PetName = "mouse" }, TestContext.Current.CancellationToken);
        var forMouse = _viewModel.SizeChoices.Select(choice => choice.Width).ToList();

        // Assert
        forDefaultPet.Should().OnlyContain(width => width == 0);
        forMouse.Should().Equal(48, 72, 96);
    }

    /// <summary>
    /// When the pets become known after the tab was made, the sizes are drawn again with the pet in use.
    /// </summary>
    [Fact]
    public async Task SizeChoices_PetsFoundLater_AreDrawnAgain()
    {
        // Arrange
        var catalog = new FakePetCatalog(PetSettings.DefaultPetName);
        var loader = new FakePetLoader();
        loader.Available.Add(PetSettings.DefaultPetName);
        var pets = new InstalledPets(catalog, loader, "pets", _ => TestPicture);
        await CreateAsync(pets);
        var before = _viewModel.SizeChoices.Select(choice => choice.Width).ToList();

        // Act
        await pets.RefreshAsync();

        // Assert
        before.Should().OnlyContain(width => width == 0);
        _viewModel.SizeChoices.Select(choice => choice.Width).Should().OnlyContain(width => width > 0);
    }

    /// <summary>
    /// A change of the installed pets draws the sizes again, but leaves what the user is typing in the rest of the tab
    /// as it is.
    /// </summary>
    [Fact]
    public async Task SizeChoices_PetsChangedWhileEditing_KeepsUnappliedEntries()
    {
        // Arrange
        var catalog = new FakePetCatalog();
        var loader = new FakePetLoader();
        loader.Available.Add(PetSettings.DefaultPetName);
        var pets = new InstalledPets(catalog, loader, "pets", _ => TestPicture);
        await CreateAsync(pets);
        _viewModel.ChatProcessesText = "My Chat";

        // Act
        catalog.Names = [PetSettings.DefaultPetName];
        await pets.RefreshAsync();

        // Assert
        _viewModel.ChatProcessesText.Should().Be("My Chat");
        _viewModel.ChatProcessesError.Should().NotBeEmpty();
        _viewModel.SizeChoices.Should().OnlyContain(choice => choice.Width > 0);
    }

    /// <summary>
    /// The sizes are made again only when something they show changed, not for every change of the settings.
    /// </summary>
    [Fact]
    public async Task SizeChoices_UnrelatedSettingChanged_AreNotMadeAgain()
    {
        // Arrange
        await CreateAsync();
        var before = _viewModel.SizeChoices;

        // Act
        await _settings.UpdateAsync(settings => settings with { ChatterEnabled = !settings.ChatterEnabled }, TestContext.Current.CancellationToken);

        // Assert
        _viewModel.SizeChoices.Should().BeSameAs(before);
    }

    /// <summary>
    /// Choosing another size makes the sizes again, once, so that the new one is marked.
    /// </summary>
    [Fact]
    public async Task SizeChoices_SizeChosen_AreMadeAgain()
    {
        // Arrange
        await CreateAsync();
        var before = _viewModel.SizeChoices;

        // Act
        _viewModel.ChooseSizeCommand.Execute(PetSize.Large);

        // Assert
        _viewModel.SizeChoices.Should().NotBeSameAs(before);
    }

    /// <summary>
    /// Pressing the button of a size stores that size, like choosing it any other way, and marks that size as the one in
    /// use at once.
    /// </summary>
    [Fact]
    public async Task ChooseSizeCommand_Executed_StoresSizeAndMarksIt()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ChooseSizeCommand.Execute(PetSize.Large);
        await _viewModel.TrySaveAsync();

        // Assert
        _settings.Current.Size.Should().Be(PetSize.Large);
        _viewModel.SizeChoices.Where(choice => choice.IsCurrent).Select(choice => choice.Size).Should().Equal(PetSize.Large);
    }

    /// <summary>
    /// The size in use is marked among the choices when the tab is made.
    /// </summary>
    [Fact]
    public async Task SizeChoices_StoredSize_IsMarkedAsCurrent()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { Size = PetSize.Small };

        // Act
        await CreateAsync();

        // Assert
        _viewModel.SizeChoices.Where(choice => choice.IsCurrent).Select(choice => choice.Size).Should().Equal(PetSize.Small);
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
        await _viewModel.TrySaveAsync();

        // Assert
        shownAtStart.Should().BeFalse();
        _settings.Current.ClimbEdges.Should().BeTrue();
    }

    /// <summary>
    /// The choice to let the pet stroll while it is busy is shown from the stored settings (on by default) and stored
    /// when changed.
    /// </summary>
    [Fact]
    public async Task StrollWhileBusy_Changed_IsStored()
    {
        // Arrange
        await CreateAsync();
        var shownAtStart = _viewModel.StrollWhileBusy;

        // Act
        _viewModel.StrollWhileBusy = false;
        await _viewModel.TrySaveAsync();

        // Assert
        shownAtStart.Should().BeTrue();
        _settings.Current.StrollWhileBusy.Should().BeFalse();
    }

    /// <summary>
    /// The choice to look for updates by itself is shown from the stored settings (on by default) and stored when
    /// changed.
    /// </summary>
    [Fact]
    public async Task AutoCheckUpdates_Changed_IsStored()
    {
        // Arrange
        await CreateAsync();
        var shownAtStart = _viewModel.AutoCheckUpdates;

        // Act
        _viewModel.AutoCheckUpdates = false;
        await _viewModel.TrySaveAsync();

        // Assert
        shownAtStart.Should().BeTrue();
        _settings.Current.AutoCheckUpdates.Should().BeFalse();
    }

    /// <summary>
    /// The end of the working day is shown as an hour and a minute, and a chosen hour or minute is applied.
    /// </summary>
    [Fact]
    public async Task ClockOutTime_Chosen_IsApplied()
    {
        // Arrange
        await CreateAsync();
        var hourAtStart = _viewModel.ClockOutHour;
        var minuteAtStart = _viewModel.ClockOutMinute;

        // Act
        _viewModel.ClockOutHour = 17;
        _viewModel.ClockOutMinute = 30;
        await _viewModel.TrySaveAsync();

        // Assert
        hourAtStart.Should().Be(18);
        minuteAtStart.Should().Be(0);
        _settings.Current.ClockOutTime.Should().Be(new TimeSpan(17, 30, 0));
    }

    /// <summary>
    /// The hours to choose from cover the whole day and the minutes are every ten minutes.
    /// </summary>
    [Fact]
    public async Task ClockOutChoices_Listed_HoursAllDayAndMinutesEveryTen()
    {
        // Arrange
        await CreateAsync();

        // Assert
        _viewModel.HourChoices.Should().Equal(Enumerable.Range(0, 24));
        _viewModel.MinuteChoices.Should().Equal(0, 10, 20, 30, 40, 50);
    }

    /// <summary>
    /// A minute left over from an older version of the settings stays shown and chosen.
    /// </summary>
    [Fact]
    public async Task ClockOutChoices_MinuteOffTheGrid_IsKept()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { ClockOutTime = new TimeSpan(17, 35, 0) };

        // Act
        await CreateAsync();

        // Assert
        _viewModel.MinuteChoices.Should().Equal(0, 10, 20, 30, 35, 40, 50);
        _viewModel.ClockOutMinute.Should().Be(35);
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
        await _viewModel.TrySaveAsync();

        // Assert
        shownAtStart.Should().Be("Zoom");
        _settings.Current.MeetingProcesses.Should().Equal("Zoom", "Slack");
        _viewModel.MeetingProcessesText.Should().Be($"Zoom{Environment.NewLine}Slack");
    }

    /// <summary>
    /// The list of chat applications is shown, and an edit is split into lines, cleaned up and shown back.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_Edited_IsCleanedAndShownBack()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { ChatProcesses = ["Weixin"] };
        await CreateAsync();
        var shownAtStart = _viewModel.ChatProcessesText;

        // Act
        _viewModel.ChatProcessesText = "Weixin.exe\r\n  Signal \n\n signal\r\n";
        await _viewModel.TrySaveAsync();

        // Assert
        shownAtStart.Should().Be("Weixin");
        _settings.Current.ChatProcesses.Should().Equal("Weixin", "Signal");
        _viewModel.ChatProcessesText.Should().Be($"Weixin{Environment.NewLine}Signal");
    }

    /// <summary>
    /// A process name with a space in it is not applied and the reason is shown.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_NameWithSpace_IsNotAppliedAndExplained()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ChatProcessesText = "Weixin\r\nMy Chat";

        // Assert
        _settings.Current.ChatProcesses.Should().Equal(PetSettings.DefaultChatProcesses);
        _viewModel.ChatProcessesError.Should().Contain("My Chat");
    }

    /// <summary>
    /// A process that is already in another list is not applied and the reason is shown.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_NameInAnotherList_IsNotAppliedAndExplained()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ChatProcessesText = "Weixin\r\nzoom";

        // Assert
        _settings.Current.ChatProcesses.Should().Equal(PetSettings.DefaultChatProcesses);
        _viewModel.ChatProcessesError.Should().Contain("zoom");
    }

    /// <summary>
    /// Fixing the text applies the list and clears the explanation.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_FixedAfterError_IsAppliedAndErrorCleared()
    {
        // Arrange
        await CreateAsync();
        _viewModel.ChatProcessesText = "Weixin\r\nMy Chat";

        // Act
        _viewModel.ChatProcessesText = "Weixin";
        await _viewModel.TrySaveAsync();

        // Assert
        _settings.Current.ChatProcesses.Should().Equal("Weixin");
        _viewModel.ChatProcessesError.Should().BeEmpty();
    }

    /// <summary>
    /// A list held back by an error keeps what the user typed while other settings change.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_HeldBackByError_KeepsTextWhenOtherSettingChanges()
    {
        // Arrange
        await CreateAsync();
        _viewModel.ChatProcessesText = "Weixin\r\nMy Chat";

        // Act
        _viewModel.ChatterEnabled = true;

        // Assert
        _viewModel.ChatProcessesText.Should().Be("Weixin\r\nMy Chat");
    }

    /// <summary>
    /// Removing a process from the list it clashes with lets the held-back list through.
    /// </summary>
    [Fact]
    public async Task MeetingProcesses_ClashRemovedFromOtherList_IsAppliedAfterwards()
    {
        // Arrange
        await CreateAsync();
        _viewModel.MeetingProcessesText = "Zoom\r\nWeixin";
        var errorBefore = _viewModel.MeetingProcessesError;

        // Act
        _viewModel.ChatProcessesText = "WhatsApp";
        await _viewModel.TrySaveAsync();

        // Assert
        errorBefore.Should().NotBeEmpty();
        _viewModel.MeetingProcessesError.Should().BeEmpty();
        _settings.Current.MeetingProcesses.Should().Equal("Zoom", "Weixin");
    }

    /// <summary>
    /// Showing the settings again after another change keeps the same list of minutes, as replacing it would clear the
    /// choice shown in the box.
    /// </summary>
    [Fact]
    public async Task ClockOutChoices_OtherSettingChanges_KeepsTheSameList()
    {
        // Arrange
        await CreateAsync();
        var before = _viewModel.MinuteChoices;

        // Act
        _viewModel.ChatterEnabled = true;

        // Assert
        _viewModel.MinuteChoices.Should().BeSameAs(before);
    }

    /// <summary>
    /// The same process in two lists of the draft is refused for the list edited last, and saving waits for a fix.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_ClashWithMeetingDraft_IsRefusedAndBlocksSaving()
    {
        // Arrange
        await CreateAsync();
        _viewModel.MeetingProcessesText = "Zoom\r\nSignal";

        // Act
        _viewModel.ChatProcessesText = "Weixin\r\nSignal";
        var saved = await _viewModel.TrySaveAsync();

        // Assert
        _viewModel.ChatProcessesError.Should().Contain("Signal");
        _viewModel.CanSave.Should().BeFalse();
        saved.Should().BeFalse();
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// An update that finishes after a newer, invalid edit does not wipe the explanation of that edit.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_EarlierUpdateFinishesLate_KeepsErrorOfNewerEdit()
    {
        // Arrange
        await CreateAsync();
        var release = new TaskCompletionSource();
        _store.SaveBlocker = release.Task;
        _viewModel.ChatterEnabled = true;
        _viewModel.ChatProcessesText = "Weixin";

        // Act
        _viewModel.ChatProcessesText = "Weixin\r\nMy Chat";
        release.SetResult();
        await WaitUntilAsync(() => _settings.Current.ChatProcesses.SequenceEqual(["Weixin"]));

        // Assert
        _viewModel.ChatProcessesError.Should().Contain("My Chat");
    }

    /// <summary>
    /// When the settings failed to load nothing is applied, so nothing is said about the list either.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_SettingsFailedToLoad_NoErrorAndNothingApplied()
    {
        // Arrange
        _store.LoadException = new IOException("broken");
        await CreateAsync();

        // Act
        _viewModel.ChatProcessesText = "Weixin\r\nSignal";

        // Assert
        _viewModel.ChatProcessesError.Should().BeEmpty();
        _settings.Current.ChatProcesses.Should().Equal(PetSettings.DefaultChatProcesses);
    }

    /// <summary>
    /// What was taken out of the lists at load is explained in the tab.
    /// </summary>
    [Fact]
    public async Task Constructor_NamesTakenOutAtLoad_AreExplained()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { MeetingProcesses = ["Zoom", "My Meeting"], BlacklistedProcesses = ["zoom", "Game"] };

        // Act
        await CreateAsync();

        // Assert
        _viewModel.ProcessListNotice.Should().Contain("My Meeting").And.Contain("zoom");
    }

    /// <summary>
    /// Nothing is said when nothing was taken out.
    /// </summary>
    [Fact]
    public async Task Constructor_NothingTakenOut_NoNotice()
    {
        // Act
        await CreateAsync();

        // Assert
        _viewModel.ProcessListNotice.Should().BeEmpty();
    }

    /// <summary>
    /// The note about names taken out at load goes on listing the lists the user has not edited, and drops the one that
    /// was edited.
    /// </summary>
    [Fact]
    public async Task ProcessListNotice_ListEdited_DropsOnlyThatListsNames()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { MeetingProcesses = ["Zoom", "My Meeting"], DevProcesses = ["Code", "My Editor"] };
        await CreateAsync();
        var before = _viewModel.ProcessListNotice;

        // Act
        _viewModel.MeetingProcessesText = "Teams";

        // Assert
        before.Should().Contain("My Meeting").And.Contain("My Editor");
        _viewModel.ProcessListNotice.Should().Contain("My Editor").And.NotContain("My Meeting");
    }

    /// <summary>
    /// Editing the list a name stayed in also drops that name from the note, as it may no longer be in it.
    /// </summary>
    [Fact]
    public async Task ProcessListNotice_KeptInListEdited_DropsTheName()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { MeetingProcesses = ["Zoom"], BlacklistedProcesses = ["zoom", "Game"] };
        await CreateAsync();
        var before = _viewModel.ProcessListNotice;

        // Act
        _viewModel.MeetingProcessesText = "Teams";

        // Assert
        before.Should().Contain("zoom");
        _viewModel.ProcessListNotice.Should().BeEmpty();
    }

    /// <summary>
    /// Once every list that lost names has been edited the note is gone.
    /// </summary>
    [Fact]
    public async Task ProcessListNotice_AllAffectedListsEdited_IsEmpty()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { MeetingProcesses = ["Zoom", "My Meeting"] };
        await CreateAsync();

        // Act
        _viewModel.MeetingProcessesText = "Teams";

        // Assert
        _viewModel.ProcessListNotice.Should().BeEmpty();
    }

    /// <summary>
    /// Editing a list that lost nothing leaves the note as it is.
    /// </summary>
    [Fact]
    public async Task ProcessListNotice_UnrelatedListEdited_IsKept()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { MeetingProcesses = ["Zoom", "My Meeting"] };
        await CreateAsync();
        var before = _viewModel.ProcessListNotice;

        // Act
        _viewModel.ChatProcessesText = "Weixin";

        // Assert
        _viewModel.ProcessListNotice.Should().Be(before).And.NotBeEmpty();
    }

    /// <summary>
    /// A list held back by a clash is accepted once the other list drops the name, even if that list is then edited
    /// again into something invalid.
    /// </summary>
    [Fact]
    public async Task ChatProcesses_EditedAwayFromClash_ReleasesHeldBackMeetingList()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { ChatProcesses = ["Signal"] };
        await CreateAsync();
        _viewModel.MeetingProcessesText = "Zoom\r\nSignal";
        var heldBack = _viewModel.MeetingProcessesError;

        // Act
        _viewModel.ChatProcessesText = "Weixin";
        _viewModel.ChatProcessesText = "Weixin\r\nMy Chat";
        _viewModel.ChatProcessesText = "Weixin";
        await _viewModel.TrySaveAsync();

        // Assert
        heldBack.Should().NotBeEmpty();
        _viewModel.MeetingProcessesError.Should().BeEmpty();
        _settings.Current.MeetingProcesses.Should().Equal("Zoom", "Signal");
        _settings.Current.ChatProcesses.Should().Equal("Weixin");
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
        await _viewModel.TrySaveAsync();

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
        await _viewModel.TrySaveAsync();

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
        await _viewModel.TrySaveAsync();

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
        _viewModel.ChatterEnabled = true;
        await _viewModel.TrySaveAsync();

        // Assert
        _viewModel.SaveError.Should().Contain("disk full");
        _settings.Current.ChatterEnabled.Should().BeTrue();
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
        _viewModel.ChatterEnabled = true;
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
        _viewModel.ChatterEnabled = true;

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
        _viewModel.SetBossKey(OtherKey);
        await _viewModel.TrySaveAsync();

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
        _viewModel.SetBossKey(OtherKey);
        await _viewModel.TrySaveAsync();

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
        _viewModel.SetBossKey(OtherKey);
        await _viewModel.TrySaveAsync();
        var another = new HotkeyGesture(HotkeyModifiers.Alt, 0x71);

        // Act
        _viewModel.SetBossKey(another);
        await _viewModel.TrySaveAsync();

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
        _viewModel.SetBossKey(PetSettings.Default.BossKey);
        await _viewModel.TrySaveAsync();

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

    /// <summary>
    /// Changing an entry only edits the draft: nothing is applied or stored before saving.
    /// </summary>
    [Fact]
    public async Task ChatterEnabled_SetBeforeSave_ChangesNothingYet()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ChatterEnabled = true;

        // Assert
        _settings.Current.ChatterEnabled.Should().BeFalse();
        _store.SaveCount.Should().Be(0);
        _viewModel.HasUnsavedChanges.Should().BeTrue();
        _viewModel.CanSave.Should().BeTrue();
        _viewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// A new tab has nothing to save.
    /// </summary>
    [Fact]
    public async Task Constructor_Fresh_HasNothingToSave()
    {
        // Act
        await CreateAsync();

        // Assert
        _viewModel.HasUnsavedChanges.Should().BeFalse();
        _viewModel.CanSave.Should().BeFalse();
        _viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// Changing an entry back to what is saved leaves nothing to save.
    /// </summary>
    [Fact]
    public async Task ChatterEnabled_BackToSavedValue_HasNothingToSave()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.ChatterEnabled = true;
        _viewModel.ChatterEnabled = false;

        // Assert
        _viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    /// <summary>
    /// The save command saves the draft.
    /// </summary>
    [Fact]
    public async Task SaveCommand_Executed_SavesTheDraft()
    {
        // Arrange
        await CreateAsync();
        _viewModel.ClimbEdges = true;

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        _settings.Current.ClimbEdges.Should().BeTrue();
        _viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    /// <summary>
    /// Reverting shows the saved values again in every entry that was edited, and clears the explanations.
    /// </summary>
    [Fact]
    public async Task Revert_AfterEdits_ShowsSavedValuesAgain()
    {
        // Arrange
        await CreateAsync();
        var toggle = _viewModel.Scenarios.Single(item => item.State == PetState.GitPush);
        _viewModel.SelectedSize = PetSize.Large;
        _viewModel.SedentaryText = "abc";
        _viewModel.MeetingProcessesText = "Zoom\r\nWeixin";
        toggle.IsEnabled = false;
        _viewModel.AutoStart = true;

        // Act
        _viewModel.RevertCommand.Execute(null);

        // Assert
        _viewModel.SelectedSize.Should().Be(PetSettings.Default.Size);
        _viewModel.SedentaryText.Should().Be("45");
        _viewModel.TimingError.Should().BeEmpty();
        _viewModel.MeetingProcessesError.Should().BeEmpty();
        _viewModel.MeetingProcessesText.Should().Be(string.Join(Environment.NewLine, PetSettings.DefaultMeetingProcesses));
        toggle.IsEnabled.Should().BeTrue();
        _viewModel.AutoStart.Should().BeFalse();
        _viewModel.HasUnsavedChanges.Should().BeFalse();
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// An entry that cannot be saved blocks the save command until it is fixed, and keeps what was typed.
    /// </summary>
    [Fact]
    public async Task SaveCommand_InvalidEntry_CannotExecuteUntilFixed()
    {
        // Arrange
        await CreateAsync();
        _viewModel.ChatterEnabled = true;

        // Act
        _viewModel.SleepText = "abc";
        var whileInvalid = _viewModel.SaveCommand.CanExecute(null);
        var shown = _viewModel.SleepText;
        _viewModel.SleepText = "10";

        // Assert
        whileInvalid.Should().BeFalse();
        shown.Should().Be("abc");
        _viewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// A change made elsewhere reaches the entries the user has not edited, and leaves the edited ones.
    /// </summary>
    [Fact]
    public async Task SelectedSize_SettingsChangedElsewhere_FollowsUntouchedEntriesAndKeepsEditedOnes()
    {
        // Arrange
        await CreateAsync();
        _viewModel.SelectedSize = PetSize.Large;

        // Act
        await _settings.UpdateAsync(settings => settings with { Size = PetSize.Small, ClimbEdges = true });

        // Assert
        _viewModel.ClimbEdges.Should().BeTrue();
        _viewModel.SelectedSize.Should().Be(PetSize.Large);
        _viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    /// <summary>
    /// An entry that cannot be saved still counts as an unsaved change, so it can be reverted and closing asks about it.
    /// </summary>
    [Fact]
    public async Task SleepText_InvalidEntryOnly_CountsAsUnsavedAndCanBeReverted()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.SleepText = "0";
        var whileInvalid = _viewModel.HasUnsavedChanges;
        _viewModel.RevertCommand.Execute(null);

        // Assert
        whileInvalid.Should().BeTrue();
        _viewModel.CanSave.Should().BeFalse();
        _viewModel.HasUnsavedChanges.Should().BeFalse();
        _viewModel.SleepText.Should().Be("5");
        _viewModel.TimingError.Should().BeEmpty();
    }

    /// <summary>
    /// The note about names removed from the process lists at startup comes back when the edited lists are reverted.
    /// </summary>
    [Fact]
    public async Task Revert_AfterEditingAListWithRemovedNames_ShowsTheNoticeAgain()
    {
        // Arrange
        _store.Stored = PetSettings.Default with { MeetingProcesses = ["Zoom", "My Chat"] };
        await CreateAsync();
        var noticeAtStart = _viewModel.ProcessListNotice;
        _viewModel.MeetingProcessesText = "Teams";
        var noticeAfterEdit = _viewModel.ProcessListNotice;

        // Act
        _viewModel.RevertCommand.Execute(null);

        // Assert
        noticeAtStart.Should().Contain("My Chat");
        noticeAfterEdit.Should().NotContain("My Chat");
        _viewModel.ProcessListNotice.Should().Be(noticeAtStart);
    }

    /// <summary>
    /// When no boss key was ever registered and the new one is refused, the settings go back to the previous key and not
    /// to the refused one.
    /// </summary>
    [Fact]
    public async Task Save_BossKeyTakenAndNoneRegisteredBefore_GoesBackToThePreviousKey()
    {
        // Arrange
        _hotkeys.Taken.Add(PetSettings.Default.BossKey);
        _hotkeys.Taken.Add(OtherKey);
        await CreateAsync();
        _viewModel.SetBossKey(OtherKey);

        // Act
        await _viewModel.TrySaveAsync();

        // Assert
        _settings.Current.BossKey.Should().Be(PetSettings.Default.BossKey);
        _store.Stored.BossKey.Should().Be(PetSettings.Default.BossKey);
        _viewModel.BossKeyText.Should().Be("Ctrl+Alt+H");
        _viewModel.BossKeyError.Should().Contain("Ctrl+Shift+F12");
    }

    /// <summary>
    /// A failure to write the corrected boss key is reported too, not only the failure of the first write.
    /// </summary>
    [Fact]
    public async Task Save_BossKeyTakenAndCorrectionNotWritten_ShowsSaveError()
    {
        // Arrange
        await CreateAsync();
        _hotkeys.Taken.Add(OtherKey);
        _viewModel.SetBossKey(OtherKey);
        _store.FailSavesAfter = 1;

        // Act
        await _viewModel.TrySaveAsync();

        // Assert
        _viewModel.SaveError.Should().Contain("disk full");
        _viewModel.BossKeyError.Should().Contain("already in use");
    }

    /// <summary>
    /// A setting changed elsewhere does not rewrite a box whose text already means the value, so typing is not disturbed.
    /// </summary>
    [Fact]
    public async Task MeetingProcessesText_SettingsChangedElsewhere_KeepsWhatIsBeingTyped()
    {
        // Arrange
        await CreateAsync();
        _viewModel.MeetingProcessesText = "Zoom\r\n";
        _viewModel.SleepText = " 12 ";

        // Act
        await _settings.UpdateAsync(settings => settings with { ClimbEdges = true });

        // Assert
        _viewModel.MeetingProcessesText.Should().Be("Zoom\r\n");
        _viewModel.SleepText.Should().Be(" 12 ");
        _viewModel.ClimbEdges.Should().BeTrue();
    }

    /// <summary>
    /// A box is rewritten when a change made elsewhere gives its entry another value.
    /// </summary>
    [Fact]
    public async Task SleepText_SettingsChangedElsewhereToAnotherValue_ShowsTheNewValue()
    {
        // Arrange
        await CreateAsync();

        // Act
        await _settings.UpdateAsync(settings => settings with { SleepAfter = TimeSpan.FromMinutes(20) });

        // Assert
        _viewModel.SleepText.Should().Be("20");
    }

    /// <summary>
    /// The boss key is only taken into the draft until saving; the system is not asked before.
    /// </summary>
    [Fact]
    public async Task SetBossKey_BeforeSave_DoesNotRegisterIt()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.SetBossKey(OtherKey);

        // Assert
        _viewModel.BossKeyText.Should().Be("Ctrl+Shift+F12");
        _viewModel.HasUnsavedChanges.Should().BeTrue();
        _hotkeys.Registrations.Should().NotContain(OtherKey);
        _settings.Current.BossKey.Should().Be(PetSettings.Default.BossKey);
    }

    /// <summary>
    /// When the boss key is refused the other changes are still saved.
    /// </summary>
    [Fact]
    public async Task Save_BossKeyTaken_StillSavesTheOtherChanges()
    {
        // Arrange
        await CreateAsync();
        _hotkeys.Taken.Add(OtherKey);
        _viewModel.ChatterEnabled = true;
        _viewModel.SetBossKey(OtherKey);

        // Act
        var saved = await _viewModel.TrySaveAsync();

        // Assert
        saved.Should().BeFalse();
        _settings.Current.ChatterEnabled.Should().BeTrue();
        _settings.Current.BossKey.Should().Be(PetSettings.Default.BossKey);
        _viewModel.BossKeyError.Should().Contain("already in use");
        _viewModel.BossKeyText.Should().Be("Ctrl+Alt+H");
    }

    /// <summary>
    /// The start-at-sign-in switch is only passed to the system when saving, and counts as an unsaved change.
    /// </summary>
    [Fact]
    public async Task AutoStart_SwitchedBeforeSave_IsNotPassedToSystemYet()
    {
        // Arrange
        await CreateAsync();

        // Act
        _viewModel.AutoStart = true;

        // Assert
        _autoStart.Enabled.Should().BeFalse();
        _viewModel.HasUnsavedChanges.Should().BeTrue();
        _viewModel.CanSave.Should().BeTrue();
    }

    /// <summary>
    /// A start-at-sign-in switch the system refuses does not stop the other changes from being saved.
    /// </summary>
    [Fact]
    public async Task Save_AutoStartRefused_StillSavesTheOtherChanges()
    {
        // Arrange
        await CreateAsync();
        _autoStart.Refusal = "policy forbids it";
        _viewModel.AutoStart = true;
        _viewModel.ChatterEnabled = true;

        // Act
        var saved = await _viewModel.TrySaveAsync();

        // Assert
        saved.Should().BeFalse();
        _settings.Current.ChatterEnabled.Should().BeTrue();
        _viewModel.AutoStart.Should().BeFalse();
        _viewModel.AutoStartError.Should().Contain("policy forbids it");
    }
}
