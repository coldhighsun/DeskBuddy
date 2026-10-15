using System.IO;
using System.Reflection;

namespace DeskBuddy.Tests;

/// <summary>
/// Locates files of the repository from tests. The build output may be redirected away from the repository, so the
/// root is baked into the test assembly at build time instead of being searched for above the output folder.
/// </summary>
internal static class RepositoryPaths
{
    /// <summary>
    /// The repository root, taken from the <c>RepositoryRoot</c> assembly metadata.
    /// </summary>
    private static readonly string Root = typeof(RepositoryPaths).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "RepositoryRoot")
        .Value!;

    /// <summary>
    /// Gets the <c>pets</c> folder shipped with the repository.
    /// </summary>
    public static string Pets => Path.Combine(Root, "pets");

    /// <summary>
    /// Builds the path of a file inside the repository.
    /// </summary>
    /// <param name="parts">
    /// The path segments below the repository root.
    /// </param>
    /// <returns>
    /// The full path.
    /// </returns>
    public static string Combine(params string[] parts)
    {
        return Path.Combine([Root, .. parts]);
    }
}
