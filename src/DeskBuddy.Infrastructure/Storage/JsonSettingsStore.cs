using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace DeskBuddy.Infrastructure.Storage;

/// <summary>
/// Stores the settings as <c>settings.json</c> in a directory. Entries missing from the file take their default value,
/// so adding a setting later does not break older files; a file that cannot be parsed is set aside as
/// <c>settings.json.bak</c> and the defaults are used, so a corrupt file is never silently overwritten.
/// </summary>
/// <param name="directory">
/// The directory that holds the data files.
/// </param>
/// <param name="logger">
/// Logger used to report a quarantined file.
/// </param>
public sealed class JsonSettingsStore(string directory, ILogger<JsonSettingsStore> logger) : ISettingsStore
{
    /// <summary>
    /// File name of the saved settings.
    /// </summary>
    public const string FileName = "settings.json";

    /// <summary>
    /// Version number written into the file, for future format changes.
    /// </summary>
    private const int FormatVersion = 1;

    /// <summary>
    /// Serializer options for the file.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Full path of the data file.
    /// </summary>
    private readonly string _path = Path.Combine(directory, FileName);

    /// <inheritdoc />
    public async Task<PetSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return PetSettings.Default;
        }

        var json = await AtomicFile.ReadAllTextAsync(_path, cancellationToken);
        SettingsFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SettingsFile>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            QuarantineCorruptFile(ex);
            return PetSettings.Default;
        }

        if (file is null)
        {
            QuarantineCorruptFile(null);
            return PetSettings.Default;
        }

        return file.ToSettings().Normalize();
    }

    /// <inheritdoc />
    public Task SaveAsync(PetSettings settings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(SettingsFile.From(settings), SerializerOptions);
        return AtomicFile.WriteAllTextAsync(_path, json, cancellationToken);
    }

    /// <summary>
    /// Moves an unreadable data file aside so the next save does not destroy it.
    /// </summary>
    /// <param name="cause">
    /// The parse error, if any.
    /// </param>
    private void QuarantineCorruptFile(Exception? cause)
    {
        var backupPath = _path + ".bak";
        File.Move(_path, backupPath, overwrite: true);
        logger.LogWarning(cause, "The settings file was unreadable and has been moved to {BackupPath}.", backupPath);
    }

    /// <summary>
    /// Shape of the JSON file. Every value is optional so that a missing entry falls back to the default.
    /// </summary>
    private sealed record SettingsFile
    {
        /// <summary>
        /// Gets the format version.
        /// </summary>
        public int Version { get; init; } = FormatVersion;

        /// <summary>
        /// Gets the disturbance mode.
        /// </summary>
        public DisturbanceMode? Mode { get; init; }

        /// <summary>
        /// Gets the pet name.
        /// </summary>
        public string? PetName { get; init; }

        /// <summary>
        /// Gets the inactivity time before sleeping.
        /// </summary>
        public TimeSpan? SleepAfter { get; init; }

        /// <summary>
        /// Gets the sedentary reminder interval.
        /// </summary>
        public TimeSpan? SedentaryReminderAfter { get; init; }

        /// <summary>
        /// Gets the boss key as text such as <c>Ctrl+Alt+H</c>.
        /// </summary>
        public string? BossKey { get; init; }

        /// <summary>
        /// Gets whether the pet hides itself for full-screen applications.
        /// </summary>
        public bool? AutoHideWhenFullscreen { get; init; }

        /// <summary>
        /// Gets whether a window covering a whole monitor counts as full screen.
        /// </summary>
        public bool? DetectCoveredMonitor { get; init; }

        /// <summary>
        /// Gets the process names that count as full screen.
        /// </summary>
        public List<string>? BlacklistedProcesses { get; init; }

        /// <summary>
        /// Gets whether the pet sits down at its computer while the user is developing.
        /// </summary>
        public bool? DevModeEnabled { get; init; }

        /// <summary>
        /// Gets the process names that count as development tools.
        /// </summary>
        public List<string>? DevProcesses { get; init; }

        /// <summary>
        /// Gets whether the pet acts out situations by itself.
        /// </summary>
        public bool? ScenariosEnabled { get; init; }

        /// <summary>
        /// Gets the names of the scenarios that are switched off.
        /// </summary>
        public List<string>? DisabledScenarios { get; init; }

        /// <summary>
        /// Gets the process names that count as meeting applications.
        /// </summary>
        public List<string>? MeetingProcesses { get; init; }

        /// <summary>
        /// Gets the time of day at which the working day ends.
        /// </summary>
        public TimeSpan? ClockOutTime { get; init; }

        /// <summary>
        /// Gets the language of the user interface, as text so that an unknown name does not spoil the whole file.
        /// </summary>
        public string? Language { get; init; }

        /// <summary>
        /// Gets the color theme of the user interface, as text so that an unknown name does not spoil the whole file.
        /// </summary>
        public string? Theme { get; init; }

        /// <summary>
        /// Gets how large the pet is shown, as text so that an unknown name does not spoil the whole file.
        /// </summary>
        public string? Size { get; init; }

        /// <summary>
        /// Gets whether the pet may climb along the edges of the screen; <see langword="null"/> in files from before the
        /// option existed.
        /// </summary>
        public bool? ClimbEdges { get; init; }

        /// <summary>
        /// Creates the file shape from settings.
        /// </summary>
        /// <param name="settings">
        /// The settings to store.
        /// </param>
        /// <returns>
        /// The file shape.
        /// </returns>
        public static SettingsFile From(PetSettings settings)
        {
            return new SettingsFile
            {
                Mode = settings.Mode,
                PetName = settings.PetName,
                SleepAfter = settings.SleepAfter,
                SedentaryReminderAfter = settings.SedentaryReminderAfter,
                BossKey = settings.BossKey.ToString(),
                AutoHideWhenFullscreen = settings.AutoHideWhenFullscreen,
                DetectCoveredMonitor = settings.DetectCoveredMonitor,
                BlacklistedProcesses = [.. settings.BlacklistedProcesses],
                DevModeEnabled = settings.DevModeEnabled,
                DevProcesses = [.. settings.DevProcesses],
                ScenariosEnabled = settings.ScenariosEnabled,
                DisabledScenarios = [.. settings.DisabledScenarios],
                MeetingProcesses = [.. settings.MeetingProcesses],
                ClockOutTime = settings.ClockOutTime,
                Language = settings.Language.ToString(),
                Theme = settings.Theme.ToString(),
                Size = settings.Size.ToString(),
                ClimbEdges = settings.ClimbEdges,
            };
        }

        /// <summary>
        /// Converts to settings, using the default for every missing or unparsable entry.
        /// </summary>
        /// <returns>
        /// The settings, not yet normalized.
        /// </returns>
        public PetSettings ToSettings()
        {
            var defaults = PetSettings.Default;
            return new PetSettings
            {
                Mode = Mode ?? defaults.Mode,
                PetName = PetName ?? defaults.PetName,
                SleepAfter = SleepAfter ?? defaults.SleepAfter,
                SedentaryReminderAfter = SedentaryReminderAfter ?? defaults.SedentaryReminderAfter,
                BossKey = HotkeyGesture.TryParse(BossKey, out var gesture) ? gesture : defaults.BossKey,
                AutoHideWhenFullscreen = AutoHideWhenFullscreen ?? defaults.AutoHideWhenFullscreen,
                DetectCoveredMonitor = DetectCoveredMonitor ?? defaults.DetectCoveredMonitor,
                BlacklistedProcesses = BlacklistedProcesses ?? defaults.BlacklistedProcesses,
                DevModeEnabled = DevModeEnabled ?? defaults.DevModeEnabled,
                DevProcesses = DevProcesses ?? defaults.DevProcesses,
                ScenariosEnabled = ScenariosEnabled ?? defaults.ScenariosEnabled,
                DisabledScenarios = DisabledScenarios ?? defaults.DisabledScenarios,
                MeetingProcesses = MeetingProcesses ?? defaults.MeetingProcesses,
                ClockOutTime = ClockOutTime ?? defaults.ClockOutTime,
                Language = Enum.TryParse<AppLanguage>(Language, ignoreCase: true, out var language) && Enum.IsDefined(language)
                    ? language
                    : defaults.Language,
                Theme = Enum.TryParse<AppTheme>(Theme, ignoreCase: true, out var theme) && Enum.IsDefined(theme)
                    ? theme
                    : defaults.Theme,
                Size = Enum.TryParse<PetSize>(Size, ignoreCase: true, out var size) && Enum.IsDefined(size)
                    ? size
                    : defaults.Size,
                ClimbEdges = ClimbEdges ?? defaults.ClimbEdges,
            };
        }
    }
}
