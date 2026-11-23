namespace DeskBuddy.Domain.Visibility;

/// <summary>
/// Decides whether the pet is on screen. There are three independent reasons for it to be hidden: the user hid it (boss
/// key or menu), which lasts until they show it again; a full-screen or presenting application being in front, which
/// lasts only as long as that application is; and the session being locked, when nobody can see the pet anyway. Keeping
/// them apart means a pet the user hid does not pop back up when a video ends or the computer is unlocked.
/// </summary>
/// <param name="timeProvider">The time source for the restore delay.</param>
/// <param name="restoreDelay">
/// How long the full-screen condition must stay gone before the pet comes back. This stops the pet from flickering
/// when a full-screen application briefly loses the foreground.
/// </param>
public sealed class PetVisibility(TimeProvider timeProvider, TimeSpan restoreDelay)
{
    /// <summary>
    /// Since when the full-screen condition has been gone, or <see langword="null"/> while it holds or the pet is not
    /// auto-hidden.
    /// </summary>
    private DateTimeOffset? _clearSince;

    /// <summary>
    /// Gets a value indicating whether the user has hidden the pet.
    /// </summary>
    public bool IsManuallyHidden
    {
        get => _isManuallyHidden;
        private set => _isManuallyHidden = value;
    }

    /// <summary>
    /// Whether the user has hidden the pet. Volatile, because the tray icon reads it from another thread than the one the
    /// pet lives on.
    /// </summary>
    private volatile bool _isManuallyHidden;

    /// <summary>
    /// Whether a full-screen application may hide the pet. Volatile, because the settings are applied from another thread
    /// than the one the pet lives on.
    /// </summary>
    private volatile bool _autoHideEnabled = true;

    /// <summary>
    /// Gets a value indicating whether the pet is hidden because a full-screen application is in front.
    /// </summary>
    public bool IsAutoHidden { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether a full-screen application may hide the pet at all. Turning it off shows
    /// a pet that was hidden that way, without waiting for the restore delay.
    /// </summary>
    public bool AutoHideEnabled
    {
        get => _autoHideEnabled;
        set => _autoHideEnabled = value;
    }

    /// <summary>
    /// Gets a value indicating whether the pet should be on screen.
    /// </summary>
    public bool IsVisible => !IsManuallyHidden && !IsAutoHidden && !IsSessionLocked;

    /// <summary>
    /// Gets a value indicating whether the session is locked (or its screen is otherwise not shown), so the pet is put
    /// away to cost nothing while nobody can see it.
    /// </summary>
    public bool IsSessionLocked { get; private set; }

    /// <summary>
    /// Feeds in whether the session is locked.
    /// </summary>
    /// <param name="locked"><see langword="true"/> while the session is locked.</param>
    /// <returns><see langword="true"/> if <see cref="IsVisible"/> changed.</returns>
    public bool SetSessionLocked(bool locked)
    {
        var wasVisible = IsVisible;
        IsSessionLocked = locked;
        return IsVisible != wasVisible;
    }

    /// <summary>
    /// Hides or shows the pet at the user's request.
    /// </summary>
    /// <param name="hidden"><see langword="true"/> to hide the pet.</param>
    /// <returns><see langword="true"/> if <see cref="IsVisible"/> changed.</returns>
    public bool SetManuallyHidden(bool hidden)
    {
        var wasVisible = IsVisible;
        IsManuallyHidden = hidden;
        return IsVisible != wasVisible;
    }

    /// <summary>
    /// Switches the user's hide on or off.
    /// </summary>
    /// <returns><see langword="true"/> if <see cref="IsVisible"/> changed.</returns>
    public bool ToggleManuallyHidden()
    {
        return SetManuallyHidden(!IsManuallyHidden);
    }

    /// <summary>
    /// Feeds in whether a full-screen application is currently in front. The pet hides at once when it is, and comes
    /// back only after it has been gone for the restore delay.
    /// </summary>
    /// <param name="fullscreenActive">
    /// <see langword="true"/> while a full-screen, presenting or blacklisted application is in front.
    /// </param>
    /// <returns><see langword="true"/> if <see cref="IsVisible"/> changed.</returns>
    public bool UpdateFullscreen(bool fullscreenActive)
    {
        var wasVisible = IsVisible;

        if (fullscreenActive && AutoHideEnabled)
        {
            IsAutoHidden = true;
            _clearSince = null;
        }
        else if (IsAutoHidden)
        {
            var now = timeProvider.GetUtcNow();
            _clearSince ??= now;
            if (!AutoHideEnabled || now - _clearSince.Value >= restoreDelay)
            {
                IsAutoHidden = false;
                _clearSince = null;
            }
        }

        return IsVisible != wasVisible;
    }
}
