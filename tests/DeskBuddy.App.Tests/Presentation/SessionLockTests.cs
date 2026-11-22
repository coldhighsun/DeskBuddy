using Microsoft.Win32;

namespace DeskBuddy.App.Tests.Presentation;

/// <summary>
/// Tests for <see cref="SessionLock"/>.
/// </summary>
public sealed class SessionLockTests
{
    /// <summary>
    /// Locking and unlocking the session are recognized.
    /// </summary>
    [Fact]
    public void IsLocked_LockAndUnlock_AreRecognized()
    {
        // Act
        var locked = SessionLock.IsLocked(SessionSwitchReason.SessionLock);
        var unlocked = SessionLock.IsLocked(SessionSwitchReason.SessionUnlock);

        // Assert
        locked.Should().BeTrue();
        unlocked.Should().BeFalse();
    }

    /// <summary>
    /// Other changes of the session leave the pet as it is.
    /// </summary>
    /// <param name="reason">The change.</param>
    [Theory]
    [InlineData(SessionSwitchReason.ConsoleConnect)]
    [InlineData(SessionSwitchReason.RemoteDisconnect)]
    [InlineData(SessionSwitchReason.SessionLogon)]
    public void IsLocked_OtherChanges_ReturnsNull(SessionSwitchReason reason)
    {
        // Act
        var result = SessionLock.IsLocked(reason);

        // Assert
        result.Should().BeNull();
    }
}
