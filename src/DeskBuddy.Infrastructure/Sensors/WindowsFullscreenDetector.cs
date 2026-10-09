using System.Runtime.InteropServices;
using DeskBuddy.Application.Abstractions;

namespace DeskBuddy.Infrastructure.Sensors;

/// <summary>
/// Detects "do not disturb" situations with the shell's <c>SHQueryUserNotificationState</c>, the same signal Windows
/// uses to decide whether to show toast notifications.
/// </summary>
public sealed partial class WindowsFullscreenDetector : IFullscreenDetector
{
    /// <inheritdoc />
    public bool IsDoNotDisturb()
    {
        return SHQueryUserNotificationState(out var state) == 0 && IsSuppressing(state);
    }

    /// <summary>
    /// Decides whether a <c>QUERY_USER_NOTIFICATION_STATE</c> value means notices should be held back.
    /// </summary>
    /// <param name="state">
    /// The state reported by the shell: 1 not present (screen saver or locked), 2 busy (full-screen app or
    /// presentation settings), 3 full-screen Direct3D app, 4 presentation mode, 5 accepts notifications,
    /// 6 quiet time (Focus Assist), 7 windowed Store app.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for states 1, 2, 3, 4 and 6; <see langword="false"/> otherwise, including unknown values.
    /// </returns>
    public static bool IsSuppressing(int state)
    {
        return state is 1 or 2 or 3 or 4 or 6;
    }

    /// <summary>
    /// Queries the current notification state of the user's session.
    /// </summary>
    /// <param name="state">
    /// Receives the <c>QUERY_USER_NOTIFICATION_STATE</c> value.
    /// </param>
    /// <returns>
    /// An HRESULT; zero on success.
    /// </returns>
    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out int state);
}
