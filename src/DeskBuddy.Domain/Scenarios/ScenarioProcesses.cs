namespace DeskBuddy.Domain.Scenarios;

/// <summary>
/// Names of the processes (without <c>.exe</c>) that signal what the user is up to, matched ignoring case.
/// </summary>
public static class ScenarioProcesses
{
    /// <summary>
    /// Gets the compilers and build tools. <c>dotnet</c> is on the list although language servers also run under it;
    /// that is why a build is only recognized together with a busy processor.
    /// </summary>
    public static IReadOnlyList<string> BuildTools { get; } =
    [
        "msbuild", "cl", "link", "cargo", "rustc", "gcc", "g++", "cc1plus", "clang", "ninja", "make", "javac",
        "gradle", "tsc", "go", "dotnet",
    ];

    /// <summary>
    /// Gets the helpers git starts to talk to a remote. They run for push, pull, fetch and clone alike, so the two
    /// cannot be told apart without reading the command line, which is not done.
    /// </summary>
    public static IReadOnlyList<string> GitRemoteHelpers { get; } = ["git-remote-https", "git-remote-http"];

    /// <summary>
    /// Gets the web browsers, where the user looks things up.
    /// </summary>
    public static IReadOnlyList<string> Browsers { get; } =
        ["chrome", "msedge", "firefox", "brave", "vivaldi", "opera"];
}
