using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.App.ViewModels;

/// <content>
/// Reminder timings, the end of the working day, chatter and the scenario switches.
/// </content>
public sealed partial class SettingsTabViewModel
{
    /// <summary>
    /// Shortest value accepted for a time setting, in minutes.
    /// </summary>
    private const int MinMinutes = 1;

    /// <summary>
    /// Names the sedentary reminder interval in the draft.
    /// </summary>
    private const string SedentaryEditKey = "Sedentary";

    /// <summary>
    /// Names the time before the pet falls asleep in the draft.
    /// </summary>
    private const string SleepEditKey = "Sleep";

    /// <summary>
    /// Whether the pet now and then says something unprompted.
    /// </summary>
    [ObservableProperty]
    private bool _chatterEnabled;

    /// <summary>
    /// The sedentary reminder interval in minutes, as typed.
    /// </summary>
    [ObservableProperty]
    private string _sedentaryText = string.Empty;

    /// <summary>
    /// The time before the pet falls asleep in minutes, as typed.
    /// </summary>
    [ObservableProperty]
    private string _sleepText = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the pet acts out situations by itself.
    /// </summary>
    [ObservableProperty]
    private bool _scenariosEnabled;

    /// <summary>
    /// Gets or sets the hour of the time of day the working day ends.
    /// </summary>
    [ObservableProperty]
    private int _clockOutHour;

    /// <summary>
    /// Gets or sets the minute of the time of day the working day ends.
    /// </summary>
    [ObservableProperty]
    private int _clockOutMinute;

    /// <summary>
    /// Gets or sets the minutes to choose from for the end of the working day: every ten minutes, plus the minute in
    /// use if an older version of the settings left it off that grid.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<int> _minuteChoices = ClockOutMinuteChoices(0);

    /// <summary>
    /// A problem with the last timing entry, or empty.
    /// </summary>
    [ObservableProperty]
    private string _timingError = string.Empty;

    /// <summary>
    /// Gets the switches for the individual scenarios.
    /// </summary>
    public IReadOnlyList<ScenarioToggleViewModel> Scenarios { get; }

    /// <summary>
    /// Applies the choice to let the pet say something unprompted now and then.
    /// </summary>
    /// <param name="value">
    /// Whether the pet may chat.
    /// </param>
    partial void OnChatterEnabledChanged(bool value)
    {
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.ChatterEnabled), settings => settings with { ChatterEnabled = value });
        }
    }

    /// <summary>
    /// Applies a typed sedentary reminder interval.
    /// </summary>
    /// <param name="value">
    /// The text entered.
    /// </param>
    partial void OnSedentaryTextChanged(string value)
    {
        CommitMinutes(
            SedentaryEditKey,
            value,
            Strings.SettingsSedentary,
            MinMinutes,
            (int)PetSettings.MaxSedentaryInterval.TotalMinutes,
            (settings, minutes) => settings with { SedentaryReminderAfter = minutes });
    }

    /// <summary>
    /// Applies a typed time before the pet falls asleep.
    /// </summary>
    /// <param name="value">
    /// The text entered.
    /// </param>
    partial void OnSleepTextChanged(string value)
    {
        CommitMinutes(
            SleepEditKey,
            value,
            Strings.SettingsSleep,
            MinMinutes,
            (int)PetSettings.MaxSleepAfter.TotalMinutes,
            (settings, minutes) => settings with { SleepAfter = minutes });
    }

    /// <summary>
    /// Applies the choice to let the pet act out situations by itself.
    /// </summary>
    /// <param name="value">
    /// Whether scenarios are on.
    /// </param>
    partial void OnScenariosEnabledChanged(bool value)
    {
        if (!_refreshing)
        {
            _editor.Set(nameof(PetSettings.ScenariosEnabled), settings => settings with { ScenariosEnabled = value });
        }
    }

    /// <summary>
    /// Gets the hours to choose from for the end of the working day.
    /// </summary>
    public IReadOnlyList<int> HourChoices { get; } = [.. Enumerable.Range(0, 24)];

    /// <summary>
    /// Applies the chosen hour at which the working day ends.
    /// </summary>
    /// <param name="value">
    /// The hour, 0 to 23.
    /// </param>
    partial void OnClockOutHourChanged(int value)
    {
        ApplyClockOutTime();
    }

    /// <summary>
    /// Applies the chosen minute at which the working day ends.
    /// </summary>
    /// <param name="value">
    /// The minute, 0 to 59.
    /// </param>
    partial void OnClockOutMinuteChanged(int value)
    {
        ApplyClockOutTime();
    }

    /// <summary>
    /// Applies the time at which the working day ends from the chosen hour and minute.
    /// </summary>
    private void ApplyClockOutTime()
    {
        if (_refreshing)
        {
            return;
        }

        var time = new TimeSpan(ClockOutHour, ClockOutMinute, 0);
        _editor.Set(nameof(PetSettings.ClockOutTime), settings => settings with { ClockOutTime = time });
    }

    /// <summary>
    /// Applies the switch of a single scenario.
    /// </summary>
    /// <param name="state">
    /// The scenario.
    /// </param>
    /// <param name="enabled">
    /// Whether it is on.
    /// </param>
    private void OnScenarioToggled(PetState state, bool enabled)
    {
        if (_refreshing)
        {
            return;
        }

        var name = state.ToString();
        _editor.Set($"Scenario.{name}", settings => settings with
        {
            DisabledScenarios =
            [
                .. settings.DisabledScenarios.Where(disabled => !string.Equals(disabled, name, StringComparison.OrdinalIgnoreCase)),
                .. enabled ? [] : new[] { name },
            ],
        });
    }

    /// <summary>
    /// Validates a number of minutes and applies it, or says what is expected.
    /// </summary>
    /// <param name="key">
    /// Names the entry in the draft.
    /// </param>
    /// <param name="text">
    /// The text entered.
    /// </param>
    /// <param name="label">
    /// The name of the entry, for the message.
    /// </param>
    /// <param name="min">
    /// The smallest allowed number of minutes.
    /// </param>
    /// <param name="max">
    /// The largest allowed number of minutes.
    /// </param>
    /// <param name="apply">
    /// Puts the duration into the settings.
    /// </param>
    private void CommitMinutes(
        string key,
        string text,
        string label,
        int min,
        int max,
        Func<PetSettings, TimeSpan, PetSettings> apply)
    {
        if (_refreshing)
        {
            return;
        }

        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || minutes < min
            || minutes > max)
        {
            _editor.SetError(key, string.Format(CultureInfo.CurrentCulture, Strings.MinutesRange, label, min, max));
            UpdateTimingError();
            return;
        }

        _editor.SetError(key, null);
        var duration = TimeSpan.FromMinutes(minutes);
        _editor.Set(key, settings => apply(settings, duration));
        UpdateTimingError();
    }

    /// <summary>
    /// Tells whether a text typed for a number of minutes already means a duration.
    /// </summary>
    /// <param name="text">
    /// The text typed.
    /// </param>
    /// <param name="duration">
    /// The duration to compare with.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the text is a number of minutes equal to the duration.
    /// </returns>
    private static bool MeansMinutes(string text, TimeSpan duration)
    {
        return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            && minutes == (int)Math.Ceiling(duration.TotalMinutes);
    }

    /// <summary>
    /// Shows the explanation of the first timing entry that cannot be saved, or nothing if they are all fine.
    /// </summary>
    private void UpdateTimingError()
    {
        var error = _editor.ErrorFor(SedentaryEditKey);
        TimingError = error.Length > 0 ? error : _editor.ErrorFor(SleepEditKey);
    }

    /// <summary>
    /// Works out the minutes to choose from for the end of the working day.
    /// </summary>
    /// <param name="current">
    /// The minute in use.
    /// </param>
    /// <returns>
    /// Every ten minutes of the hour, with the current minute added if it is not one of them.
    /// </returns>
    private static IReadOnlyList<int> ClockOutMinuteChoices(int current)
    {
        return [.. Enumerable.Range(0, 6).Select(step => step * 10).Append(current).Distinct().Order()];
    }

    /// <summary>
    /// Formats a duration as whole minutes, rounding up so a short duration never shows as zero.
    /// </summary>
    /// <param name="duration">
    /// The duration.
    /// </param>
    /// <returns>
    /// The number of minutes.
    /// </returns>
    private static string Minutes(TimeSpan duration)
    {
        return ((int)Math.Ceiling(duration.TotalMinutes)).ToString(CultureInfo.InvariantCulture);
    }
}
