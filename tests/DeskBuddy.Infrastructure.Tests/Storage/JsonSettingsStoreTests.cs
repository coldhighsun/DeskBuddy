using DeskBuddy.Domain.Settings;
using DeskBuddy.Infrastructure.Storage;

namespace DeskBuddy.Infrastructure.Tests.Storage;

/// <summary>
/// Tests for <see cref="JsonSettingsStore"/>, using a real temporary directory per test.
/// </summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    /// <summary>
    /// Temporary data directory, unique per test instance. It does not exist until something is saved.
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DeskBuddySettings_{Guid.NewGuid():N}");

    /// <summary>
    /// The store under test.
    /// </summary>
    private readonly JsonSettingsStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSettingsStoreTests"/> class.
    /// </summary>
    public JsonSettingsStoreTests()
    {
        _store = new JsonSettingsStore(_directory, NullLogger<JsonSettingsStore>.Instance);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// Gets the path of the data file.
    /// </summary>
    private string DataPath => Path.Combine(_directory, JsonSettingsStore.FileName);

    /// <summary>
    /// Writes raw content as the data file.
    /// </summary>
    /// <param name="content">
    /// The file content.
    /// </param>
    /// <returns>
    /// A task that completes when the file is written.
    /// </returns>
    private async Task WriteRawAsync(string content)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(DataPath, content);
    }

    /// <summary>
    /// With nothing saved yet the defaults are returned and no file or directory is created by reading.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NothingSaved_ReturnsDefaultsWithoutCreatingFiles()
    {
        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.Should().Be(PetSettings.Default);
        Directory.Exists(_directory).Should().BeFalse();
    }

    /// <summary>
    /// Every setting survives a round trip.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAllValues()
    {
        // Arrange
        var settings = new PetSettings
        {
            ChatterEnabled = true,
            PetName = "猫咪",
            SleepAfter = TimeSpan.FromMinutes(8),
            SedentaryReminderAfter = TimeSpan.FromMinutes(60),
            BossKey = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B),
            AutoHideWhenFullscreen = false,
            DetectCoveredMonitor = true,
            BlacklistedProcesses = ["Game", "Teams"],
            DevProcesses = ["Code", "zed"],
            ScenariosEnabled = false,
            DisabledScenarios = ["BuildDone", "Reading"],
            MeetingProcesses = ["Zoom", "Slack"],
            ChatProcesses = ["Weixin", "Signal"],
            ClockOutTime = new TimeSpan(17, 30, 0),
            Language = AppLanguage.En,
            Theme = AppTheme.Dark,
            Size = PetSize.Large,
            ClimbEdges = true,
            StrollWhileBusy = false,
            AutoCheckUpdates = false,
        };

        // Act
        await _store.SaveAsync(settings, TestContext.Current.CancellationToken);
        var loaded = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        loaded.Should().BeEquivalentTo(settings);
    }

    /// <summary>
    /// The file is readable by hand: enums and the shortcut are text, durations are time spans, Chinese is literal.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WritesReadableFile()
    {
        // Act
        await _store.SaveAsync(PetSettings.Default with { ChatterEnabled = true, PetName = "猫咪" }, TestContext.Current.CancellationToken);

        // Assert
        var json = await File.ReadAllTextAsync(DataPath, TestContext.Current.CancellationToken);
        json.Should().Contain("\"chatterEnabled\": true");
        json.Should().Contain("\"bossKey\": \"Ctrl+Alt+H\"");
        json.Should().Contain("猫咪");
    }

    /// <summary>
    /// A completed save leaves no temporary file behind.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Completed_LeavesNoTemporaryFile()
    {
        // Act
        await _store.SaveAsync(PetSettings.Default, TestContext.Current.CancellationToken);

        // Assert
        Directory.GetFiles(_directory).Select(Path.GetFileName).Should().Equal(JsonSettingsStore.FileName);
    }

    /// <summary>
    /// Entries missing from the file take their default value, so older files keep working when settings are added.
    /// </summary>
    [Fact]
    public async Task LoadAsync_PartialFile_FillsMissingValuesWithDefaultsAndMissingListsWithNothing()
    {
        // Arrange
        await WriteRawAsync("""{"version":1,"chatterEnabled":true}""");

        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.Should().BeEquivalentTo(DefaultsWithoutLists with { ChatterEnabled = true });
    }

    /// <summary>
    /// A file from before the roaming mode was removed still loads: the old mode entry is ignored and the rest is kept.
    /// </summary>
    [Fact]
    public async Task LoadAsync_FileWithRemovedModeEntry_IgnoresItAndKeepsTheRest()
    {
        // Arrange
        await WriteRawAsync("""{"version":1,"mode":"Roam","petName":"cat"}""");

        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.Should().BeEquivalentTo(DefaultsWithoutLists with { PetName = "cat" });
    }

    /// <summary>
    /// Once a settings file exists its lists are what the user has: a list missing from it is empty, not the default
    /// one, so an older file never gets new default entries that could clash with its own lists.
    /// </summary>
    [Fact]
    public async Task LoadAsync_FileWithoutChatList_ChatListIsEmptyAndOthersAreKept()
    {
        // Arrange
        await WriteRawAsync("""{"meetingProcesses":["Zoom","Weixin"]}""");

        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.ChatProcesses.Should().BeEmpty();
        settings.MeetingProcesses.Should().Equal("Zoom", "Weixin");
        settings.DevProcesses.Should().BeEmpty();
    }

    /// <summary>
    /// Without a settings file the defaults apply, lists included.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NoFile_ListsAreTheDefaultOnes()
    {
        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.ChatProcesses.Should().Equal(PetSettings.DefaultChatProcesses);
        settings.MeetingProcesses.Should().Equal(PetSettings.DefaultMeetingProcesses);
        settings.DevProcesses.Should().Equal(PetSettings.DefaultDevProcesses);
    }

    /// <summary>
    /// Gets the defaults as a file that exists loads them: the lists the file does not have are empty.
    /// </summary>
    private static PetSettings DefaultsWithoutLists => PetSettings.Default with
    {
        BlacklistedProcesses = [],
        DevProcesses = [],
        MeetingProcesses = [],
        ChatProcesses = [],
        DisabledScenarios = [],
    };

    /// <summary>
    /// A hand-edited value outside the allowed range is corrected on load.
    /// </summary>
    [Fact]
    public async Task LoadAsync_OutOfRangeValue_IsNormalized()
    {
        // Arrange
        await WriteRawAsync("""{"sleepAfter":"00:00:01","blacklistedProcesses":["Game.exe","game"]}""");

        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.SleepAfter.Should().Be(PetSettings.MinSleepAfter);
        settings.BlacklistedProcesses.Should().Equal("Game");
    }

    /// <summary>
    /// A shortcut that cannot be parsed falls back to the default instead of discarding the whole file.
    /// </summary>
    [Fact]
    public async Task LoadAsync_UnparsableBossKey_FallsBackToDefaultKey()
    {
        // Arrange
        await WriteRawAsync("""{"chatterEnabled":true,"bossKey":"nonsense"}""");

        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.BossKey.Should().Be(PetSettings.Default.BossKey);
        settings.ChatterEnabled.Should().BeTrue();
    }

    /// <summary>
    /// A file that is not valid JSON is set aside untouched and the defaults are used.
    /// </summary>
    [Fact]
    public async Task LoadAsync_CorruptFile_MovesItToBackupAndReturnsDefaults()
    {
        // Arrange
        await WriteRawAsync("{ this is not json");

        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.Should().Be(PetSettings.Default);
        File.Exists(DataPath).Should().BeFalse();
        (await File.ReadAllTextAsync(DataPath + ".bak", TestContext.Current.CancellationToken)).Should().Be("{ this is not json");
    }

    /// <summary>
    /// A theme that is not known, as a name or as a number, falls back to following the system without spoiling the
    /// rest of the file.
    /// </summary>
    /// <param name="theme">
    /// The stored value.
    /// </param>
    [Theory]
    [InlineData("Purple")]
    [InlineData("42")]
    public async Task LoadAsync_UnknownThemeName_FallsBackToAuto(string theme)
    {
        // Arrange
        await WriteRawAsync($$"""{"version":1,"chatterEnabled":true,"theme":"{{theme}}"}""");

        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.Theme.Should().Be(AppTheme.Auto);
        settings.ChatterEnabled.Should().BeTrue();
    }

    /// <summary>
    /// A value of the wrong type (a duration that is not one) makes the file count as corrupt.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ValueOfTheWrongType_IsQuarantined()
    {
        // Arrange
        await WriteRawAsync("""{"sleepAfter":"Banana"}""");

        // Act
        var settings = await _store.LoadAsync(TestContext.Current.CancellationToken);

        // Assert
        settings.Should().Be(PetSettings.Default);
        File.Exists(DataPath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// A file locked for the whole retry period is reported to the caller instead of being waited on forever.
    /// </summary>
    [Fact]
    public async Task LoadAsync_FileLockedForGood_ThrowsIOException()
    {
        // Arrange
        await _store.SaveAsync(PetSettings.Default, TestContext.Current.CancellationToken);
        using var holder = new FileStream(DataPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var act = () => _store.LoadAsync();

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }
}
