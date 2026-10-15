using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.Domain.Scenarios;

namespace DeskBuddy.App.ViewModels;

/// <content>
/// Full-screen detection and the lists of running programs that tell what the user is doing.
/// </content>
public sealed partial class SettingsTabViewModel
{
    /// <summary>
    /// How to reach the text and the explanation of each process list, as the lists are handled alike.
    /// </summary>
    private readonly Dictionary<ProcessListKind, ProcessListBox> _processLists;

    /// <summary>
    /// What was taken out of the process lists when the settings were loaded.
    /// </summary>
    private readonly IReadOnlyList<RemovedProcessName> _removedAtLoad;

    /// <summary>
    /// The process lists the user has changed since, whose removed names the notice no longer lists.
    /// </summary>
    private readonly HashSet<ProcessListKind> _editedLists = [];

    /// <summary>
    /// How many times each process list was edited, indexed by <see cref="ProcessListKind"/>, to tell whether an edit
    /// is still the newest when its update finishes.
    /// </summary>
    private readonly int[] _processListEdits = new int[Enum.GetValues<ProcessListKind>().Length];

    /// <summary>
    /// Whether the pet hides for full-screen applications.
    /// </summary>
    [ObservableProperty]
    private bool _autoHideWhenFullscreen;

    /// <summary>
    /// Whether a window covering a whole monitor counts as full screen.
    /// </summary>
    [ObservableProperty]
    private bool _detectCoveredMonitor;

    /// <summary>
    /// The blacklisted process names, one per line.
    /// </summary>
    [ObservableProperty]
    private string _blacklistText = string.Empty;

    /// <summary>
    /// Gets or sets the explanation shown when the full-screen list cannot be used; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _blacklistError = string.Empty;

    /// <summary>
    /// Gets or sets the development tools, one process name per line.
    /// </summary>
    [ObservableProperty]
    private string _devProcessesText = string.Empty;

    /// <summary>
    /// Gets or sets the explanation shown when the list of development tools cannot be used; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _devProcessesError = string.Empty;

    /// <summary>
    /// Gets or sets the meeting applications, one process name per line.
    /// </summary>
    [ObservableProperty]
    private string _meetingProcessesText = string.Empty;

    /// <summary>
    /// Gets or sets the explanation shown when the list of meeting applications cannot be used; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _meetingProcessesError = string.Empty;

    /// <summary>
    /// Gets or sets the chat applications, one process name per line.
    /// </summary>
    [ObservableProperty]
    private string _chatProcessesText = string.Empty;

    /// <summary>
    /// Gets or sets the explanation shown when the list of chat applications cannot be used; empty when it is fine.
    /// </summary>
    [ObservableProperty]
    private string _chatProcessesError = string.Empty;

    /// <summary>
    /// Applies the full-screen auto-hide choice.
    /// </summary>
    /// <param name="value">
    /// Whether the pet hides for full-screen applications.
    /// </param>
    partial void OnAutoHideWhenFullscreenChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { AutoHideWhenFullscreen = value });
        }
    }

    /// <summary>
    /// Applies the monitor-cover detection choice.
    /// </summary>
    /// <param name="value">
    /// Whether a window covering a monitor counts as full screen.
    /// </param>
    partial void OnDetectCoveredMonitorChanged(bool value)
    {
        if (!_refreshing)
        {
            _ = ApplyAsync(settings => settings with { DetectCoveredMonitor = value });
        }
    }

    /// <summary>
    /// Applies the edited list of full-screen programs.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnBlacklistTextChanged(string value)
    {
        if (!_refreshing)
        {
            ApplyProcessList(ProcessListKind.Fullscreen, value);
        }
    }

    /// <summary>
    /// Applies the edited list of meeting applications.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnMeetingProcessesTextChanged(string value)
    {
        if (!_refreshing)
        {
            ApplyProcessList(ProcessListKind.Meeting, value);
        }
    }

    /// <summary>
    /// Applies the edited list of chat applications.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnChatProcessesTextChanged(string value)
    {
        if (!_refreshing)
        {
            ApplyProcessList(ProcessListKind.Chat, value);
        }
    }

    /// <summary>
    /// Gets or sets what was taken out of the process lists when the settings were loaded, for the user to know; empty
    /// if nothing was. Names drop out of it once the user edits their list or the one they stayed in.
    /// </summary>
    [ObservableProperty]
    private string _processListNotice = string.Empty;

    /// <summary>
    /// Applies the edited list of development tools.
    /// </summary>
    /// <param name="value">
    /// The text entered, one process name per line.
    /// </param>
    partial void OnDevProcessesTextChanged(string value)
    {
        if (!_refreshing)
        {
            ApplyProcessList(ProcessListKind.Development, value);
        }
    }

    /// <summary>
    /// Applies an edited process list, unless a name is in another list; then it is not applied and the reason is shown
    /// until the text is fixed.
    /// </summary>
    /// <param name="kind">
    /// The list.
    /// </param>
    /// <param name="text">
    /// The text entered, one process name per line.
    /// </param>
    private void ApplyProcessList(ProcessListKind kind, string text)
    {
        _ = ApplyProcessListAsync(kind, text);
    }

    /// <summary>
    /// Checks and applies an edited process list. The check is made once on what is shown and once more inside the
    /// update, on the settings as they are when it runs, as an earlier save may still have been under way before. A
    /// newer edit of the same list takes over the explanation, so an update that finishes late does not overwrite it.
    /// </summary>
    /// <param name="kind">
    /// The list.
    /// </param>
    /// <param name="text">
    /// The text entered, one process name per line.
    /// </param>
    /// <returns>
    /// A task that completes when the list has been applied or refused.
    /// </returns>
    private async Task ApplyProcessListAsync(ProcessListKind kind, string text)
    {
        var edit = ++_processListEdits[(int)kind];
        var names = ProcessListRules.Clean(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        var check = ProcessListRules.Check(_settings.Current, kind, names);
        if (check.IsValid)
        {
            var ran = false;
            var refused = check;
            await ApplyAsync(settings =>
            {
                ran = true;
                refused = ProcessListRules.Check(settings, kind, names);
                return refused.IsValid ? ProcessListRules.With(settings, kind, names) : settings;
            });
            if (!ran)
            {
                // The settings failed to load, so nothing was applied and there is nothing to say about the list.
                return;
            }

            check = refused;
        }

        if (edit == _processListEdits[(int)kind])
        {
            _processLists[kind].SetError(check.IsValid ? string.Empty : Describe(check));
        }

        if (check.IsValid)
        {
            // The list in use changed, whether or not this is still the newest edit, so what it held back may fit now.
            _editedLists.Add(kind);
            UpdateProcessListNotice();
            RecheckProcessLists(kind);
        }
    }

    /// <summary>
    /// Checks the lists held back by an error again after another list changed, as the change may have removed the
    /// clash.
    /// </summary>
    /// <param name="changed">
    /// The list that was just applied.
    /// </param>
    private void RecheckProcessLists(ProcessListKind changed)
    {
        foreach (var kind in ProcessListRules.All.Where(kind => kind != changed && _processLists[kind].GetError().Length > 0).ToList())
        {
            ApplyProcessList(kind, _processLists[kind].GetText());
        }
    }

    /// <summary>
    /// Shows what was taken out at load, leaving out the names whose list, or the list they stayed in, the user has
    /// edited since: the note may no longer be true for them.
    /// </summary>
    private void UpdateProcessListNotice()
    {
        ProcessListNotice = DescribeRemoved([.. _removedAtLoad.Where(item => !_editedLists.Contains(item.List) && (item.KeptIn is null || !_editedLists.Contains(item.KeptIn.Value)))]);
    }

    /// <summary>
    /// Tells what was taken out of the process lists at load.
    /// </summary>
    /// <param name="removed">
    /// The names taken out.
    /// </param>
    /// <returns>
    /// A heading and one line for each name; empty if nothing was taken out.
    /// </returns>
    private static string DescribeRemoved(IReadOnlyList<RemovedProcessName> removed)
    {
        if (removed.Count == 0)
        {
            return string.Empty;
        }

        var lines = removed.Select(item => item.Reason == ProcessListProblem.ContainsSpace
            ? string.Format(CultureInfo.CurrentCulture, Strings.ProcessRemovedHasSpace, item.Name, Strings.ProcessListName(item.List))
            : string.Format(CultureInfo.CurrentCulture, Strings.ProcessRemovedInOtherList, item.Name, Strings.ProcessListName(item.List), Strings.ProcessListName(item.KeptIn!.Value)));
        return string.Join(Environment.NewLine, [Strings.ProcessesRemovedNotice, .. lines]);
    }

    /// <summary>
    /// Explains why a process list cannot be used.
    /// </summary>
    /// <param name="check">
    /// The failed check.
    /// </param>
    /// <returns>
    /// The explanation.
    /// </returns>
    private static string Describe(ProcessListCheck check)
    {
        return check.Problem == ProcessListProblem.ContainsSpace
            ? string.Format(CultureInfo.CurrentCulture, Strings.ProcessNameHasSpace, check.Name)
            : string.Format(CultureInfo.CurrentCulture, Strings.ProcessNameInOtherList, check.Name, Strings.ProcessListName(check.OtherList!.Value));
    }

    /// <summary>
    /// Shows a process list from the settings, unless the text typed for it is held back by an error: that text is
    /// still being fixed and must not be replaced by the list in use.
    /// </summary>
    /// <param name="kind">
    /// The list.
    /// </param>
    /// <param name="settings">
    /// The settings to show.
    /// </param>
    private void ShowProcessList(ProcessListKind kind, PetSettings settings)
    {
        var list = _processLists[kind];
        if (list.GetError().Length == 0)
        {
            list.SetText(string.Join(Environment.NewLine, ProcessListRules.Get(settings, kind)));
        }
    }

    /// <summary>
    /// Reaches the text and the explanation of one process list.
    /// </summary>
    /// <param name="GetText">
    /// Reads the text typed.
    /// </param>
    /// <param name="SetText">
    /// Shows a text.
    /// </param>
    /// <param name="GetError">
    /// Reads the explanation shown.
    /// </param>
    /// <param name="SetError">
    /// Shows an explanation.
    /// </param>
    private sealed record ProcessListBox(Func<string> GetText, Action<string> SetText, Func<string> GetError, Action<string> SetError);
}
