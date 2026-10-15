namespace DeskBuddy.Domain.Settings;

/// <summary>
/// The lists of process names the user can edit.
/// </summary>
public enum ProcessListKind
{
    /// <summary>
    /// Programs that count as full screen whenever they are in front.
    /// </summary>
    Fullscreen,

    /// <summary>
    /// Development tools.
    /// </summary>
    Development,

    /// <summary>
    /// Meeting applications.
    /// </summary>
    Meeting,

    /// <summary>
    /// Chat applications.
    /// </summary>
    Chat,
}

/// <summary>
/// What is wrong with an edited list of process names.
/// </summary>
public enum ProcessListProblem
{
    /// <summary>
    /// Nothing; the list can be used.
    /// </summary>
    None,

    /// <summary>
    /// A process name contains a space.
    /// </summary>
    ContainsSpace,

    /// <summary>
    /// A process name is already in another list.
    /// </summary>
    InAnotherList,
}

/// <summary>
/// The outcome of checking an edited list of process names.
/// </summary>
/// <param name="Problem">
/// What is wrong, or <see cref="ProcessListProblem.None"/>.
/// </param>
/// <param name="Name">
/// The first offending process name; empty if there is no problem.
/// </param>
/// <param name="OtherList">
/// For <see cref="ProcessListProblem.InAnotherList"/>, the list that already holds the name.
/// </param>
public readonly record struct ProcessListCheck(ProcessListProblem Problem, string Name = "", ProcessListKind? OtherList = null)
{
    /// <summary>
    /// Gets a value indicating whether the list can be used.
    /// </summary>
    public bool IsValid => Problem == ProcessListProblem.None;
}

/// <summary>
/// A process name that was taken out of a list when the settings were loaded, because it breaks the rules.
/// </summary>
/// <param name="Name">
/// The process name.
/// </param>
/// <param name="List">
/// The list it was taken out of.
/// </param>
/// <param name="Reason">
/// Why: a space in the name, or being in a list that matters more.
/// </param>
/// <param name="KeptIn">
/// For <see cref="ProcessListProblem.InAnotherList"/>, the list the process stays in.
/// </param>
public readonly record struct RemovedProcessName(string Name, ProcessListKind List, ProcessListProblem Reason, ProcessListKind? KeptIn);

/// <summary>
/// The settings after the rules were enforced, and what that took out.
/// </summary>
/// <param name="Settings">
/// The settings that follow the rules.
/// </param>
/// <param name="Removed">
/// The process names that were taken out, in the order they were found.
/// </param>
public readonly record struct ProcessListEnforcement(PetSettings Settings, IReadOnlyList<RemovedProcessName> Removed);

/// <summary>
/// Rules for the process lists in the settings: names are written without spaces, and one process can only be in one
/// list, as it could not mean two things at once.
/// </summary>
public static class ProcessListRules
{
    /// <summary>
    /// Gets every list, in the order they are shown.
    /// </summary>
    public static IReadOnlyList<ProcessListKind> All { get; } =
        [ProcessListKind.Development, ProcessListKind.Meeting, ProcessListKind.Chat, ProcessListKind.Fullscreen];

    /// <summary>
    /// The lists from the one that matters most to the one that matters least, for settings that put a process in
    /// several lists: a meeting application wins over a chat application, as the pet then keeps still and quiet, and
    /// what makes the pet act wins over what only decides when it hides.
    /// </summary>
    private static readonly ProcessListKind[] Precedence =
        [ProcessListKind.Meeting, ProcessListKind.Chat, ProcessListKind.Development, ProcessListKind.Fullscreen];

    /// <summary>
    /// Makes settings follow the rules of the settings screen: names with a space in them are taken out, and a process in
    /// several lists stays in the one that matters most. From most to least important the lists are: meeting
    /// applications, chat applications, development tools, full-screen programs. The settings screen does not allow
    /// either, but a settings file from an older version or edited by hand may hold them.
    /// </summary>
    /// <param name="settings">
    /// The settings, with the lists already cleaned up by <see cref="Clean"/>.
    /// </param>
    /// <returns>
    /// The settings that follow the rules, and the names that were taken out.
    /// </returns>
    public static ProcessListEnforcement Enforce(PetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var result = settings;
        var removed = new List<RemovedProcessName>();
        var taken = new Dictionary<string, ProcessListKind>(StringComparer.OrdinalIgnoreCase);
        foreach (var kind in Precedence)
        {
            var names = Get(result, kind);
            var kept = new List<string>(names.Count);
            foreach (var name in names)
            {
                if (HasSpace(name))
                {
                    removed.Add(new RemovedProcessName(name, kind, ProcessListProblem.ContainsSpace, null));
                }
                else if (taken.TryGetValue(name, out var holder))
                {
                    removed.Add(new RemovedProcessName(name, kind, ProcessListProblem.InAnotherList, holder));
                }
                else
                {
                    kept.Add(name);
                    taken[name] = kind;
                }
            }

            if (kept.Count != names.Count)
            {
                result = With(result, kind, kept);
            }
        }

        return new ProcessListEnforcement(result, removed);
    }

    /// <summary>
    /// Tells whether a process name has a space (or other white space) in it.
    /// </summary>
    /// <param name="name">
    /// The process name.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if it does.
    /// </returns>
    private static bool HasSpace(string name)
    {
        return name.Any(char.IsWhiteSpace);
    }

    /// <summary>
    /// Cleans up the lines the user typed: trims them, strips <c>.exe</c>, drops blank lines and repeats.
    /// </summary>
    /// <param name="lines">
    /// The names as typed.
    /// </param>
    /// <returns>
    /// The cleaned names.
    /// </returns>
    public static IReadOnlyList<string> Clean(IReadOnlyList<string> lines)
    {
        return PetSettings.NormalizeProcessNames(lines);
    }

    /// <summary>
    /// Gets one of the lists from the settings.
    /// </summary>
    /// <param name="settings">
    /// The settings.
    /// </param>
    /// <param name="kind">
    /// The list.
    /// </param>
    /// <returns>
    /// The names in the list.
    /// </returns>
    public static IReadOnlyList<string> Get(PetSettings settings, ProcessListKind kind)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return kind switch
        {
            ProcessListKind.Fullscreen => settings.BlacklistedProcesses,
            ProcessListKind.Development => settings.DevProcesses,
            ProcessListKind.Meeting => settings.MeetingProcesses,
            ProcessListKind.Chat => settings.ChatProcesses,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>
    /// Returns a copy of the settings with one of the lists replaced.
    /// </summary>
    /// <param name="settings">
    /// The settings.
    /// </param>
    /// <param name="kind">
    /// The list to replace.
    /// </param>
    /// <param name="names">
    /// The new names.
    /// </param>
    /// <returns>
    /// The changed settings.
    /// </returns>
    public static PetSettings With(PetSettings settings, ProcessListKind kind, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return kind switch
        {
            ProcessListKind.Fullscreen => settings with { BlacklistedProcesses = names },
            ProcessListKind.Development => settings with { DevProcesses = names },
            ProcessListKind.Meeting => settings with { MeetingProcesses = names },
            ProcessListKind.Chat => settings with { ChatProcesses = names },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>
    /// Checks a cleaned list the user wants to use for one of the lists.
    /// </summary>
    /// <param name="settings">
    /// The settings holding the other lists.
    /// </param>
    /// <param name="kind">
    /// The list being edited.
    /// </param>
    /// <param name="names">
    /// The cleaned names, see <see cref="Clean"/>.
    /// </param>
    /// <returns>
    /// The first problem found, or a valid result.
    /// </returns>
    public static ProcessListCheck Check(PetSettings settings, ProcessListKind kind, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(names);

        foreach (var name in names)
        {
            if (HasSpace(name))
            {
                return new ProcessListCheck(ProcessListProblem.ContainsSpace, name);
            }

            foreach (var other in All.Where(other => other != kind))
            {
                if (Get(settings, other).Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    return new ProcessListCheck(ProcessListProblem.InAnotherList, name, other);
                }
            }
        }

        return new ProcessListCheck(ProcessListProblem.None);
    }
}
